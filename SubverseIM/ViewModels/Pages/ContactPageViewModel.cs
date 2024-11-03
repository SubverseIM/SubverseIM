using Avalonia;
using Avalonia.Media;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels.Components;
using System;
using System.Collections.Generic;
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
            ContactsList = new() { new ContactViewModel(serviceManager,
                new SubverseContact { DisplayName = "IsaMorphic" }) };
        }

        public async Task LoadContactsAsync(CancellationToken cancellationToken = default) 
        {
            IDbService db = await ServiceManager.GetWithAwaitAsync<IDbService>(cancellationToken);
            foreach (SubverseContact contact in db.GetContacts()) 
            {
                ContactViewModel vm = new(ServiceManager, contact);
                ContactsList.Add(vm);
            }

            foreach (ContactViewModel vm in ContactsList) 
            {
                await vm.LoadPhotoAsync(cancellationToken);
            }
        }
    }
}
