using PgpCore;
using SubverseIM.Core;
using SubverseIM.Core.Storage.Blobs;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services.Implementation
{
    public class BlobService : IBlobService
    {
        private class SequentialReadStream : Stream
        {
            private readonly Stream inner;

            private readonly IProgress<long>? progress;

            private readonly bool leaveOpen;

            private bool disposedValue;

            public SequentialReadStream(Stream inner, IProgress<long>? progress = null, bool leaveOpen = false) 
            { 
                this.inner = inner;
                this.progress = progress;
                this.leaveOpen = leaveOpen;
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => inner.Length;

            public override long Position { get => inner.Position; set => throw new NotSupportedException(); }

            public override void Flush()
            {
                inner.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int result = inner.Read(buffer, offset, count);
                progress?.Report(Position);
                return result;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new NotSupportedException();
            }

            protected override void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    disposedValue = true;

                    if (disposing && !leaveOpen)
                    {
                        inner.Dispose();
                    }
                }

                base.Dispose(disposing);
            }
        }

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

            public async Task<BlobStoreDetails> StoreAsync(IBlobSource<FileInfo> source, IProgress<float>? progress = null, CancellationToken cancellationToken = default)
            {
                FileInfo sourceFileInfo = await source.RetrieveAsync(cancellationToken);
                IProgress<long>? sourceFileProgress = progress is null ? null : 
                    new Progress<long>(x => progress.Report(x * 100f / sourceFileInfo.Length));

                using (FileStream sourceFileStream = sourceFileInfo.OpenRead())
                using (SequentialReadStream sourceReadStream = new SequentialReadStream(
                    sourceFileStream, progress: sourceFileProgress, leaveOpen: true))
                using (StreamContent sourceFileContent = new StreamContent(sourceReadStream))
                {
                    SubversePeerId peerId = await bootstrapperService.GetPeerIdAsync(cancellationToken);
                    EncryptionKeys keyContainer = await bootstrapperService.GetPeerKeysAsync(peerId, cancellationToken);

                    using (HttpResponseMessage response = await httpClient.PostAsync(new Uri(hostAddress, $"blob/store?p={peerId}"), sourceFileContent))
                    {
                        response.EnsureSuccessStatusCode();

                        string decryptedResponseStr, encryptedResponseStr = 
                            await response.Content.ReadAsStringAsync(cancellationToken);
                        using (PGP pgp = new PGP(keyContainer))
                        {
                            decryptedResponseStr = await pgp.DecryptAsync(encryptedResponseStr);
                        }

                        BlobStoreDetails? blobStoreDetails = JsonSerializer
                            .Deserialize<BlobStoreDetails>(decryptedResponseStr);
                        Debug.Assert(blobStoreDetails is not null);
                        return blobStoreDetails;
                    }
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
