﻿using SubverseIM.Core;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.ViewModels.Components;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels.Pages
{
    public class CreateContactPageViewModel : PageViewModelBase<CreateContactPageViewModel>
    {
        public override string Title => "Edit Contact";

        public override bool HasSidebar => false;

        public ContactViewModel? Contact { get; private set; }

        public CreateContactPageViewModel(IServiceManager serviceManager) : base(serviceManager)
        {
        }

        public async Task InitializeAsync(Uri contactUri, CancellationToken cancellationToken = default)
        {
            IDbService dbService = await ServiceManager.GetWithAwaitAsync<IDbService>();
            SubversePeerId otherPeer = SubversePeerId.FromString(contactUri.DnsSafeHost);
            Contact = new(ServiceManager, null, dbService.GetContact(otherPeer) ??
                new SubverseContact() { OtherPeer = otherPeer });
            await Contact.LoadPhotoAsync(cancellationToken);
        }
    }
}