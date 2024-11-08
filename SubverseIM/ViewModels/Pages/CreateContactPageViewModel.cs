using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels.Components;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels.Pages
{
    public class CreateContactPageViewModel : PageViewModelBase
    {
        public ContactViewModel? ContactViewModel { get; private set; }

        public CreateContactPageViewModel(IServiceManager serviceManager) : base(serviceManager)
        {
        }

        public async Task InitializeAsync(Uri contactUri, CancellationToken cancellationToken = default) 
        {
            IDbService dbService = await ServiceManager.GetWithAwaitAsync<IDbService>();

            SubversePeerId otherPeer = SubversePeerId.FromString(contactUri.DnsSafeHost);
            ContactViewModel = new(ServiceManager, dbService.GetContact(otherPeer));

            await ContactViewModel.LoadPhotoAsync(cancellationToken);
        }
    }
}