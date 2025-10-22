using PgpCore;
using SubverseIM.Core;
using SubverseIM.Core.Storage.Blobs;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services.Implementation
{
    public class BlobService : IBlobService
    {
        private class FileSource : IBlobSource<FileInfo>
        {
            private readonly string filePath;

            public FileSource(string filePath)
            {
                this.filePath = filePath;
            }

            public Task<FileInfo> RetrieveAsync(CancellationToken cancellationToken = default)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return Task.FromCanceled<FileInfo>(cancellationToken);
                }
                else
                {
                    return Task.FromResult(new FileInfo(filePath));
                }
            }
        }

        private class FileStore : IBlobStore<FileInfo>
        {
            private readonly IBootstrapperService bootstrapperService;

            private readonly HttpClient httpClient;

            private readonly Uri hostAddress;

            public FileStore(IBootstrapperService bootstrapperService, HttpClient httpClient, Uri hostAddress)
            {
                this.bootstrapperService = bootstrapperService;
                this.httpClient = httpClient;
                this.hostAddress = hostAddress;
            }

            public async Task<BlobStoreDetails?> StoreAsync(IBlobSource<FileInfo> source, IProgress<float>? progress = null, CancellationToken cancellationToken = default)
            {
                FileInfo sourceFileInfo = await source.RetrieveAsync(cancellationToken);
                if (sourceFileInfo.Exists)
                {
                    using (FileStream sourceFileStream = sourceFileInfo.OpenRead())
                    using (StreamContent sourceFileContent = new StreamContent(sourceFileStream))
                    {
                        SubversePeerId peerId = await bootstrapperService.GetPeerIdAsync(cancellationToken);
                        EncryptionKeys keyContainer = await bootstrapperService.GetPeerKeysAsync(peerId, cancellationToken);

                        BlobStoreDetails? blobStoreDetails;
                        using (HttpResponseMessage response = await httpClient.PostAsync(new Uri(hostAddress, $"blob/store?p={peerId}"), sourceFileContent))
                        {
                            response.EnsureSuccessStatusCode();

                            string decryptedResponseStr;
                            using (PGP pgp = new PGP(keyContainer))
                            {
                                string encryptedResponseStr = await response.Content.ReadAsStringAsync(cancellationToken);
                                decryptedResponseStr = await pgp.DecryptAsync(encryptedResponseStr);
                            }

                            blobStoreDetails = JsonSerializer.Deserialize<BlobStoreDetails>(decryptedResponseStr);
                        }

                        return blobStoreDetails;
                    }
                }
                else
                {
                    return null;
                }
            }
        }

        private readonly ConcurrentDictionary<Uri, FileStore> fileStoreMap;

        private readonly IBootstrapperService bootstrapperService;

        private readonly HttpClient httpClient;

        public BlobService(IBootstrapperService bootstrapperService, HttpClient httpClient)
        {
            fileStoreMap = new ConcurrentDictionary<Uri, FileStore>();
            this.bootstrapperService = bootstrapperService;
            this.httpClient = httpClient;
        }

        public Task<IBlobSource<FileInfo>> GetFileSourceAsync(string filePath, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<IBlobSource<FileInfo>>(cancellationToken);
            }
            else
            {
                return Task.FromResult<IBlobSource<FileInfo>>(new FileSource(filePath));
            }
        }

        public Task<IBlobStore<FileInfo>> GetFileStoreAsync(Uri hostAddress, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromCanceled<IBlobStore<FileInfo>>(cancellationToken);
            }
            else
            {
                FileStore result = fileStoreMap.GetOrAdd(hostAddress, x => new FileStore(bootstrapperService, httpClient, x));
                return Task.FromResult<IBlobStore<FileInfo>>(result);
            }
        }
    }
}
