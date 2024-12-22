using SubverseIM.Models;
using SubverseIM.ViewModels.Pages;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface IFrontendService
    {
        bool NavigatePreviousView();

        void NavigateLaunchedUri();

        void NavigateContactView(MessagePageViewModel? parentOrNull = null);

        void NavigateContactView(SubverseContact contact);

        void NavigateMessageView(IEnumerable<SubverseContact> contacts, string? topicName = null);

        Task RunOnceBackgroundAsync();

        Task RunOnceAsync(CancellationToken cancellationToken = default);
    }
}
