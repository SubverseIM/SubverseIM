using SubverseIM.Core.Storage.Blobs;
using SubverseIM.ViewModels;
using System.IO;

namespace SubverseIM.Headless.Components
{
    public class UploadTaskViewModel : ViewModelBase
    {
        private readonly IBlobStore<FileInfo> backingStore;

        public UploadTaskViewModel(IBlobStore<FileInfo> backingStore) 
        {
            this.backingStore = backingStore;
        }
    }
}
