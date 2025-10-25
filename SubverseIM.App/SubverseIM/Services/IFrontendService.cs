using SubverseIM.Models;
using SubverseIM.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface IFrontendService : IRunnable, IBackgroundRunnable
    {
        Task RestorePurchasesAsync();

        Task PromptForPurchaseAsync();

        Task<bool> NavigatePreviousViewAsync(bool shouldForceNavigation);

        Task NavigateLaunchedUriAsync(Uri? overrideUri = null);

        Task NavigateContactViewAsync(MessagePageViewModel? parentOrNull = null);

        Task NavigateContactViewAsync(SubverseContact contact);

        Task NavigateMessageViewAsync(IEnumerable<SubverseContact> contacts, string? topicName = null);

        Task NavigateTorrentViewAsync();

        Task NavigateConfigViewAsync();

        Task NavigatePurchaseViewAsync();

        Task<IReadOnlyList<Uri>> ShowUploadDialogAsync(string sourceFilePath);
    }
}
