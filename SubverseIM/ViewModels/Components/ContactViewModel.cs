using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ReactiveUI;
using SubverseIM.Models;
using SubverseIM.Services;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels.Components
{
    public class ContactViewModel : ViewModelBase
    {
        private readonly IServiceManager serviceManager;

        private readonly SubverseContact innerContact;

        private Bitmap? contactPhoto;
        public Bitmap? ContactPhoto 
        { 
            get => contactPhoto;
            private set => this.RaiseAndSetIfChanged(ref contactPhoto, value); 
        }

        public string? DisplayName => innerContact.DisplayName;

        public string? UserNote => innerContact.UserNote;

        public ContactViewModel(IServiceManager serviceManager, SubverseContact innerContact)
        {
            this.serviceManager = serviceManager;
            this.innerContact = innerContact;
        }

        public async Task LoadPhotoAsync(CancellationToken cancellationToken = default) 
        {
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>(cancellationToken);

            Stream contactPhotoStream;
            if (innerContact.ImagePath is null)
            {
                contactPhotoStream = AssetLoader.Open(
                    new System.Uri("avares://SubverseIM/Assets/izzy.jpg")
                    );
            }
            else 
            {
                contactPhotoStream = dbService.GetStream(innerContact.ImagePath);
            }

            ContactPhoto = Bitmap.DecodeToWidth(contactPhotoStream, 96);
        }
    }
}
