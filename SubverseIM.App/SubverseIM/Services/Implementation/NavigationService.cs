using Avalonia.Controls;
using Avalonia.Threading;
using MonoTorrent;
using SubverseIM.Models;
using SubverseIM.ViewModels.Pages;
using SubverseIM.Views.Pages;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Web;

namespace SubverseIM.Services.Implementation
{
    public class NavigationService : INavigationService
    {
        private readonly IServiceManager serviceManager;

        private readonly ConfigPageViewModel configPage;

        private readonly ContactPageViewModel contactPage;

        private readonly CreateContactPageViewModel createContactPage;

        private readonly PurchasePageViewModel purchasePage;

        private readonly TorrentPageViewModel torrentPage;

        private PageViewModelBase currentPage;

        public NavigationService(IServiceManager serviceManager) 
        {
            this.serviceManager = serviceManager;

            configPage = new(serviceManager);
            contactPage = new(serviceManager);
            createContactPage = new(serviceManager);
            purchasePage = new(serviceManager);
            torrentPage = new(serviceManager);

            currentPage = contactPage;
        }

        public async Task<bool> NavigatePreviousViewAsync(bool shouldForceNavigation)
        {
            bool confirm;
            if (!shouldForceNavigation && currentPage.ShouldConfirmBackNavigation)
            {
                ILauncherService launcherService = await serviceManager.GetWithAwaitAsync<ILauncherService>();
                confirm = await launcherService.ShowConfirmationDialogAsync(
                    "Confirm Navigation",
                    "Are you sure you want to go back? Unsaved changes may be lost."
                    );
            }
            else
            {
                confirm = true;
            }

            INavigation navigation = await serviceManager.GetWithAwaitAsync<INavigation>();
            if (confirm && navigation.CanGoBack)
            {
                await navigation.PopAsync();
                return true;
            }
            else
            {
                return false;
            }
        }

        public async Task NavigateConfigViewAsync()
        {
            INavigation navigation = await serviceManager.GetWithAwaitAsync<INavigation>();
            await navigation.PushAsync(new ConfigPageView() { DataContext = configPage });
            currentPage = configPage;
        }

        public async Task NavigateContactViewAsync(MessagePageViewModel? parentOrNull = null)
        {
            INavigation navigation = await serviceManager.GetWithAwaitAsync<INavigation>();
            await navigation.PushAsync(new ContactPageView() { DataContext = contactPage });

            contactPage.Parent = parentOrNull;
            contactPage.IsSidebarOpen = false;

            currentPage = contactPage;
        }

        public async Task NavigateContactViewAsync(SubverseContact contact)
        {
            if (currentPage is MessagePageViewModel messagePageViewModel)
            {
                messagePageViewModel.ShouldRefreshContacts = false;
            }
            await createContactPage.InitializeAsync(new Uri($"sv://{contact.OtherPeer}?name={HttpUtility.UrlEncode(contact.DisplayName)}"));

            INavigation navigation = await serviceManager.GetWithAwaitAsync<INavigation>();
            await navigation.PushAsync(new CreateContactPageView() { DataContext = createContactPage });
        }

        public async Task NavigateLaunchedUriAsync(Uri? overrideUri = null)
        {
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
            ILauncherService launcherService = await serviceManager.GetWithAwaitAsync<ILauncherService>();
            ITorrentService torrentService = await serviceManager.GetWithAwaitAsync<ITorrentService>();
            Uri? launchedUri = overrideUri ?? launcherService.GetLaunchedUri();

            INavigation navigation = await serviceManager.GetWithAwaitAsync<INavigation>();
            switch (launchedUri?.Scheme)
            {
                case "sv":
                    await createContactPage.InitializeAsync(launchedUri);
                    await navigation.PushAsync(new CreateContactPageView { DataContext = createContactPage });
                    currentPage = createContactPage;
                    break;
                case "magnet":
                    if (MagnetLink.TryParse(launchedUri.OriginalString, out MagnetLink? magnetLink))
                    {
                        SubverseTorrent torrent = new SubverseTorrent(
                            magnetLink.InfoHashes.V1OrV2,
                            launchedUri.OriginalString
                            );
                        await dbService.InsertOrUpdateItemAsync(torrent);

                        await torrentService.AddTorrentAsync(magnetLink.InfoHashes.V1OrV2);
                        await torrentService.StartAsync(torrent);

                        await navigation.PushAsync(new TorrentPageView { DataContext = torrentPage });
                        currentPage = torrentPage;
                    }
                    break;
                case "":
                case null:
                    await navigation.PushAsync(new ContactPageView { DataContext = contactPage });
                    break;
            }
        }

        public async Task NavigateMessageViewAsync(IEnumerable<SubverseContact> contacts, string? topicName = null)
        {
            MessagePageViewModel vm = new MessagePageViewModel(serviceManager, contacts);
            if (topicName is null)
            {
                await vm.InitializeAsync();
            }
            else
            {
                vm.TopicsList.Add(topicName);
                Dispatcher.UIThread.Post(() =>
                    vm.SendMessageTopicName = topicName,
                    DispatcherPriority.Input
                    );
            }

            INavigation navigation = await serviceManager.GetWithAwaitAsync<INavigation>();
            await navigation.PushAsync(new MessagePageView { DataContext = vm });
            currentPage = vm;
        }

        public async Task NavigatePurchaseViewAsync()
        {
            INavigation navigation = await serviceManager.GetWithAwaitAsync<INavigation>();
            await navigation.PushAsync(new PurchasePageView() { DataContext = purchasePage });
            currentPage = purchasePage;
        }

        public async Task NavigateTorrentViewAsync()
        {
            INavigation navigation = await serviceManager.GetWithAwaitAsync<INavigation>();
            await navigation.PushAsync(new TorrentPageView() { DataContext = torrentPage });
            currentPage = torrentPage;
        }

        public async Task<IReadOnlyList<Uri>> ShowUploadDialogAsync(string sourceFilePath)
        {
            UploadPageViewModel uploadPageView = new UploadPageViewModel(serviceManager, sourceFilePath);
            INavigation navigation = await serviceManager.GetWithAwaitAsync<INavigation>();
            await navigation.PushModalAsync(new UploadPageView { DataContext = uploadPageView });

            IReadOnlyList<Uri> resultUris = await uploadPageView.GetUriListAsync();
            await navigation.PopModalAsync();

            return resultUris;
        }
    }
}
