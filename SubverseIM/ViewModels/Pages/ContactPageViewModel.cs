using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels.Components;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels.Pages
{
    public class ContactPageViewModel : PageViewModelBase
    {
        public ObservableCollection<ContactViewModel> ContactsList { get; }

        public ContactPageViewModel(IServiceManager serviceManager) : base(serviceManager)
        {
            ContactsList = new();
        }

        public async Task LoadContactsAsync(CancellationToken cancellationToken = default) 
        {
            IDbService db = await ServiceManager.GetWithAwaitAsync<IDbService>(cancellationToken);
            foreach (SubverseContact contact in db.GetContacts()) 
            {
                ContactViewModel vm = new(ServiceManager, contact);
                await vm.LoadPhotoAsync();
                ContactsList.Add(vm);
            }
        }
    }
}
