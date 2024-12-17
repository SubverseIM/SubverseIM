using Avalonia.Platform.Storage;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels.Components;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels.Pages
{
    public class CreateContactPageViewModel : PageViewModelBase
    {
        public override string Title => "Edit Contact";

        public ContactViewModel? Contact { get; private set; }

        public CreateContactPageViewModel(IServiceManager serviceManager) : base(serviceManager)
        {
        }

        public async Task<bool> InitializeAsync(Uri contactUri, CancellationToken cancellationToken = default)
        {
            IDbService dbService = await ServiceManager.GetWithAwaitAsync<IDbService>();
            IPeerService peerService = await ServiceManager.GetWithAwaitAsync<IPeerService>();

            SubversePeerId otherPeer = SubversePeerId.FromString(contactUri.DnsSafeHost);
            if (otherPeer == peerService.ThisPeer)
            {
                return false;
            }
            else
            {
                Contact = new(ServiceManager, null, dbService.GetContact(otherPeer) ??
                    new SubverseContact() { OtherPeer = otherPeer });

                await Contact.LoadPhotoAsync(cancellationToken);
                return true;
            }
        }
    }
}