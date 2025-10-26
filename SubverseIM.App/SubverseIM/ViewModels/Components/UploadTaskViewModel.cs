using Avalonia.Threading;
using ReactiveUI;
using SubverseIM.Core.Storage.Blobs;
using SubverseIM.ViewModels;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Headless.Components
{
    public class UploadTaskViewModel : ViewModelBase
    {
        private readonly IBlobStore<FileInfo> backingStore;

        private bool isSelected;
        public bool IsSelected 
        {
            get => isSelected;
            set => this.RaiseAndSetIfChanged(ref isSelected, value);
        }

        private BlobStoreDetails? storeDetails;
        public BlobStoreDetails? StoreDetails
        {
            get => storeDetails;
            set => this.RaiseAndSetIfChanged(ref storeDetails, value);
        }

        private float? uploadProgress;
        public float? UploadProgress
        {
            get => uploadProgress;
            set => this.RaiseAndSetIfChanged(ref uploadProgress, value);
        }

        public UploadTaskViewModel(IBlobStore<FileInfo> backingStore) 
        {
            this.backingStore = backingStore;
        }

        public async Task<BlobStoreDetails> InitializeAsync(CancellationToken cancellationToken = default)
        {
            return await Dispatcher.UIThread.InvokeAsync(async () => 
            { 
                return StoreDetails = await backingStore.GetDetailsAsync(cancellationToken);
            });
        }

        public Task<BlobStoreResponse> UploadBlobAsync(IBlobSource<FileInfo> source, CancellationToken cancellationToken = default) 
        {
            return Task.Run(() => backingStore.StoreBlobAsync(source, 
                new Progress<float>(progress => 
                {
                    Dispatcher.UIThread.Post(() => 
                    {
                        UploadProgress = progress;
                    }, DispatcherPriority.Background);
                }), 
                cancellationToken));
        }
    }
}
