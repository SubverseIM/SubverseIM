using Avalonia.Threading;
using ReactiveUI;
using SubverseIM.Core.Storage.Blobs;
using SubverseIM.Headless.Components;
using SubverseIM.Models;
using SubverseIM.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels.Pages
{
    public class UploadPageViewModel : PageViewModelBase<UploadPageViewModel>
    {
        private readonly TaskCompletionSource<IReadOnlyList<Uri>> resultUriListTcs;

        private readonly string sourceFilePath;

        public override bool HasSidebar => false;

        public override bool ShouldConfirmBackNavigation => true;

        public override string Title => "Upload Attachment";

        private bool canUpload;
        public bool CanUpload
        {
            get => canUpload;
            set => this.RaiseAndSetIfChanged(ref canUpload, value);
        }

        public ObservableCollection<UploadTaskViewModel> UploadTasks { get; }

        public UploadPageViewModel(IServiceManager serviceManager, string sourceFilePath) : base(serviceManager)
        {
            this.sourceFilePath = sourceFilePath;
            resultUriListTcs = new();

            UploadTasks = new();
        }

        public async Task<IReadOnlyList<Uri>> GetUriListAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await resultUriListTcs.Task.WaitAsync(cancellationToken);
        }

        public async Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            IBlobService blobService = await ServiceManager.GetWithAwaitAsync<IBlobService>(cancellationToken);
            IBlobSource<FileInfo> source = await blobService.GetFileSourceAsync(sourceFilePath, cancellationToken);
            long sourceFileSize = (await source.RetrieveAsync(cancellationToken)).Length;

            IConfigurationService configurationService = await ServiceManager.GetWithAwaitAsync<IConfigurationService>(cancellationToken);
            SubverseConfig config = await configurationService.GetConfigAsync(cancellationToken);

            foreach (Uri bootstrapperUri in config.BootstrapperUriList?.Select(x => new Uri(x)) ?? [])
            {
                IBlobStore<FileInfo> backingStore = await blobService.GetFileStoreAsync(bootstrapperUri, cancellationToken);
                UploadTaskViewModel uploadTask = new UploadTaskViewModel(backingStore);
                try
                {
                    using CancellationTokenSource cts = new(3000);
                    BlobStoreDetails storeDetails = await uploadTask.InitializeAsync(cts.Token);
                    if (storeDetails.FileSizeLimit >= sourceFileSize)
                    {
                        await Dispatcher.UIThread.InvokeAsync(() => UploadTasks.Add(uploadTask), DispatcherPriority.Loaded);
                    }
                }
                catch (OperationCanceledException) { }
                catch (HttpRequestException) { }
            }

            CanUpload = UploadTasks.Count > 0;
        }

        public async Task UploadCommand()
        {
            ILauncherService launcherService = await ServiceManager.GetWithAwaitAsync<ILauncherService>();
            UploadTaskViewModel[] selectedUploadTasks = UploadTasks.Where(x => x.IsSelected).ToArray();
            if (selectedUploadTasks.Length == 0)
            {
                await launcherService.ShowAlertDialogAsync("Warning", "You must select at least one upload destination.");
                return;
            }
            else
            {
                CanUpload = false;
            }

            IBlobService blobService = await ServiceManager.GetWithAwaitAsync<IBlobService>();
            IBlobSource<FileInfo> source = await blobService.GetFileSourceAsync(sourceFilePath);
            BlobStoreResponse[] responses = await Task.WhenAll(selectedUploadTasks.Select(uploadTask => uploadTask.UploadBlobAsync(source)));

            List<Uri> resultUris = new();
            foreach ((BlobStoreDetails storeDetails, BlobStoreResponse response) in selectedUploadTasks.Select(x => x.StoreDetails!).Zip(responses))
            {
                string blobHashStr = Convert.ToHexStringLower(response.BlobHash);
                string secretKeyStr = Convert.ToHexStringLower(response.SecretKey);

                Uri hostAddress = new Uri(storeDetails.HostAddress);
                resultUris.Add(new Uri(hostAddress, $"blob/{blobHashStr}?psk={secretKeyStr}"));
            }

            resultUriListTcs.SetResult(resultUris);
        }

        public Task CancelCommand()
        {
            resultUriListTcs.SetResult(Array.Empty<Uri>());
            return Task.CompletedTask;
        }
    }
}
