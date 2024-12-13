using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ReactiveUI;
using SubverseIM.Models;
using SubverseIM.Services;
using System.Collections.Generic;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using SubverseIM.Services.Implementation;
using Avalonia.Platform.Storage;
using System.Linq;
using SubverseIM.ViewModels.Pages;

namespace SubverseIM.ViewModels.Components
{
    public class ContactViewModel : ViewModelBase
    {
        private const double HEX_ANGLE = Math.Tau / 6.0;

        private static readonly Geometry hexagonPath;

        private static readonly IList<Point> hexagonPoints;

        private static IList<Point> GenerateHexagon(double r)
        {
            List<Point> points = new();
            for (int i = 0; i < 6; i++)
            {
                double theta = HEX_ANGLE * i;
                (double y, double x) = Math.SinCos(theta);
                points.Add(new((x + 1.0) * r, (y + 1.0) * r));
            }

            return points;
        }

        static ContactViewModel()
        {
            hexagonPoints = GenerateHexagon(32);
            hexagonPath = new PolylineGeometry(GenerateHexagon(31), true);
        }

        internal readonly IServiceManager serviceManager;

        internal readonly ContactPageViewModel? contactPageView;

        internal readonly SubverseContact innerContact;

        private bool isSelected;
        public bool IsSelected 
        {
            get => isSelected;
            set 
            {
                this.RaiseAndSetIfChanged(ref isSelected, value);
            }
        }

        private bool shouldShowOptions;
        public bool ShouldShowOptions 
        {
            get => shouldShowOptions;
            set 
            {
                this.RaiseAndSetIfChanged(ref shouldShowOptions, value);
            }
        }

        private Bitmap? contactPhoto;
        public Bitmap? ContactPhoto
        {
            get => contactPhoto;
            private set => this.RaiseAndSetIfChanged(ref contactPhoto, value);
        }

        public string? DisplayName
        {
            get => innerContact.DisplayName;
            set
            {
                innerContact.DisplayName = value;
                this.RaisePropertyChanged();
            }
        }

        public string? UserNote
        {
            get => innerContact.UserNote;
            set
            {
                innerContact.UserNote = value;
                this.RaisePropertyChanged();
            }
        }

        public string? ImagePath
        {
            get => innerContact.ImagePath;
            set
            {
                innerContact.ImagePath = value;
                this.RaisePropertyChanged();
            }
        }

        public IList<Point> HexagonPoints => hexagonPoints;

        public Geometry HexagonPath => hexagonPath;

        public ContactViewModel(IServiceManager serviceManager, ContactPageViewModel? contactPageView, SubverseContact innerContact)
        {
            this.serviceManager = serviceManager;
            this.contactPageView = contactPageView;
            this.innerContact = innerContact;
        }

        public async Task LoadPhotoAsync(CancellationToken cancellationToken = default)
        {
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>(cancellationToken);

            Stream? contactPhotoStream = null;
            if (innerContact.ImagePath is not null)
            {
                dbService.TryGetReadStream(innerContact.ImagePath, out contactPhotoStream);
            }

            ContactPhoto = Bitmap.DecodeToHeight(contactPhotoStream ??
                AssetLoader.Open(new Uri("avares://SubverseIM/Assets/logo.png")),
                64);
        }

        public async Task ChangePhotoCommandAsync()
        {
            IStorageProvider storageProvider = await serviceManager.GetWithAwaitAsync<IStorageProvider>();
            IReadOnlyList<IStorageFile> files = await storageProvider.OpenFilePickerAsync(
                new FilePickerOpenOptions
                {
                    AllowMultiple = false,
                    FileTypeFilter = [FilePickerFileTypes.ImageJpg],
                    Title = "Choose Avatar for Contact"
                });

            if (files.Count == 0) return;

            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
            using (Stream imageFileStream = await files.Single().OpenReadAsync())
            {
                ContactPhoto = Bitmap.DecodeToHeight(imageFileStream, 64);
            }
        }

        public async Task SaveChangesCommandAsync()
        {
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
            if (ContactPhoto is not null)
            {
                using Stream dbFileStream = dbService.CreateWriteStream(
                    innerContact.ImagePath = $"$/img/{innerContact.OtherPeer}.jpg"
                    );
                ContactPhoto.Save(dbFileStream);
            }
            dbService.InsertOrUpdateItem(innerContact);

            IFrontendService frontendService = await serviceManager.GetWithAwaitAsync<IFrontendService>();
            frontendService.NavigateContactView();
        }

        public async Task EditCommandAsync()
        {
            IFrontendService frontendService = await serviceManager.GetWithAwaitAsync<IFrontendService>();
            frontendService.NavigateContactView(innerContact);
        }

        public async Task DeleteCommandAsync() 
        {
            ILauncherService launcherService = await serviceManager.GetWithAwaitAsync<ILauncherService>();
            if (await launcherService.ShowConfirmationDialogAsync("Delete this Contact?", "Are you sure you want to delete this contact?"))
            {
                IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
                dbService.DeleteItemById<SubverseContact>(innerContact.Id);
                contactPageView?.ContactsList.Remove(this);
            }
        }

        public async Task CancelCommandAsync() 
        {
            IFrontendService frontendService = await serviceManager.GetWithAwaitAsync<IFrontendService>();
            frontendService.NavigateContactView();
        }
    }
}
