using SubverseIM.Headless.Components;
using SubverseIM.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels.Pages
{
    public class UploadPageViewModel : PageViewModelBase<UploadPageViewModel>
    {
        private readonly TaskCompletionSource<IReadOnlyList<Uri>> resultUriListTcs;

        public override bool HasSidebar => false;

        public override bool ShouldConfirmBackNavigation => true;

        public override string Title => "Upload Attachment";

        public ObservableCollection<UploadTaskViewModel> UploadTasks { get; } 

        public UploadPageViewModel(IServiceManager serviceManager) : base(serviceManager)
        {
            resultUriListTcs = new();
            UploadTasks = new();
        }

        public async Task<IReadOnlyList<Uri>> GetUriListAsync(CancellationToken cancellationToken = default) 
        {
            cancellationToken.ThrowIfCancellationRequested();
            return await resultUriListTcs.Task.WaitAsync(cancellationToken);
        }
    }
}
