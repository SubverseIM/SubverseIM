using Avalonia.Platform.Storage;
using SubverseIM.Models;
using SubverseIM.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface IFrontendService : IRunnable, IBackgroundRunnable
    {
        bool NavigatePreviousView();

        void NavigateLaunchedUri(Uri? overrideUri = null);

        void NavigateContactView(MessagePageViewModel? parentOrNull = null);

        void NavigateContactView(SubverseContact contact);

        void NavigateMessageView(IEnumerable<SubverseContact> contacts, string? topicName = null);

        void NavigateTorrentView();
    }
}
