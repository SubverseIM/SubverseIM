using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels.Components;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels.Pages
{
    public class ContactPageViewModel : PageViewModelBase
    {
        public override string Title => "Contacts View";

        public ObservableCollection<ContactViewModel> ContactsList { get; }

        public ContactPageViewModel(IServiceManager serviceManager) : base(serviceManager)
        {
            ContactsList = new();
        }

        public async Task LoadContactsAsync(CancellationToken cancellationToken = default) 
        {
            ContactsList.Clear();

            IDbService dbService = await ServiceManager.GetWithAwaitAsync<IDbService>(cancellationToken);
            foreach (SubverseContact contact in dbService.GetContacts()) 
            {
                ContactViewModel vm = new(ServiceManager, this, contact);
                await vm.LoadPhotoAsync();
                ContactsList.Add(vm);
            }
        }

        public async Task InviteCommandAsync() 
        {
            IPeerService peerService = await ServiceManager.GetWithAwaitAsync<IPeerService>();
            await peerService.SendInviteAsync();
        }

        public async Task MessageCommandAsync() 
        {
            IEnumerable<SubverseContact> contacts = ContactsList
                .Where(x => x.IsSelected)
                .Select(x => x.innerContact);

            if (contacts.Any())
            {
                IFrontendService frontendService = await ServiceManager.GetWithAwaitAsync<IFrontendService>();
                frontendService.NavigateMessageView(contacts);
            }
            else 
            {
                ILauncherService launcherService = await ServiceManager.GetWithAwaitAsync<ILauncherService>();
                await launcherService.ShowAlertDialogAsync("Note", "You must select at least one contact to start a conversation.");
            }
        }
    }
}
