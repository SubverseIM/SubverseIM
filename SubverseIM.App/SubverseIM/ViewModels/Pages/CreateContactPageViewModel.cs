using SubverseIM.Core;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels.Components;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace SubverseIM.ViewModels.Pages
{
    public class CreateContactPageViewModel : PageViewModelBase<CreateContactPageViewModel>
    {
        public override string Title => "Edit Contact";

        public override bool ShouldConfirmBackNavigation => Contact?.innerContact.Id is not null;

        public override bool HasSidebar => false;

        public ContactViewModel? Contact { get; private set; }

        public CreateContactPageViewModel(IServiceManager serviceManager) : base(serviceManager)
        {
        }

        public async Task InitializeAsync(Uri contactUri, CancellationToken cancellationToken = default)
        {
            IDbService dbService = await ServiceManager.GetWithAwaitAsync<IDbService>(cancellationToken);
            SubversePeerId otherPeer = SubversePeerId.FromString(contactUri.DnsSafeHost);
            Contact = new(ServiceManager, null, await dbService.GetContactAsync(otherPeer, cancellationToken) ?? 
                new SubverseContact() 
                { 
                    OtherPeer = otherPeer, 
                    DisplayName = HttpUtility.ParseQueryString(contactUri.Query)["name"],
                });
            await Contact.LoadPhotoAsync(cancellationToken);
        }
    }
}