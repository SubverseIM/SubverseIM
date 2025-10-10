using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using ReactiveUI;
using SubverseIM.Models;
using SubverseIM.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace SubverseIM.ViewModels.Components
{
    public class ContactViewModel : ViewModelBase
    {
        private const string DELETE_CONFIRM_TITLE = "Delete topic messages?";
        private const string DELETE_CONFIRM_MESSAGE = "Warning: all messages labeled with this topic will be permanently deleted! Are you sure you want to proceed?";

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

        internal readonly IContactContainer? contactContainer;

        internal readonly SubverseContact innerContact;

        public string? TopicName => innerContact.TopicName;

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
            get => innerContact.TopicName ?? innerContact.DisplayName;
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

        public Color BubbleColor
        {
            get
            {
                if (innerContact.ChatColorCode.HasValue)
                {
                    return Color.FromUInt32(innerContact.ChatColorCode.Value);
                }
                else
                {
                    return HsvColor.ToRgb(RandomNumberGenerator.GetInt32(360), 1.0, 0.5);
                }
            }
            set
            {
                innerContact.ChatColorCode = value.ToUInt32();
                this.RaisePropertyChanged();
            }
        }

        public IList<Point> HexagonPoints => hexagonPoints;

        public Geometry HexagonPath => hexagonPath;

        public ContactViewModel(IServiceManager serviceManager, IContactContainer? contactContainer, SubverseContact innerContact)
        {
            this.serviceManager = serviceManager;
            this.contactContainer = contactContainer;
            this.innerContact = innerContact;
        }

        public async Task LoadPhotoAsync(CancellationToken cancellationToken = default)
        {
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>(cancellationToken);

            Stream? contactPhotoStream = null;
            if (innerContact.ImagePath is not null)
            {
                contactPhotoStream = await dbService.GetReadStreamAsync(innerContact.ImagePath, cancellationToken);
            }

            ContactPhoto = Bitmap.DecodeToHeight(contactPhotoStream ??
                AssetLoader.Open(new Uri("avares://SubverseIM/Assets/logo.png")),
                64);
        }

        public async Task ChangePhotoCommand()
        {
            TopLevel topLevel = await serviceManager.GetWithAwaitAsync<TopLevel>();
            IReadOnlyList<IStorageFile> files = await topLevel.StorageProvider.OpenFilePickerAsync(
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

        public async Task SaveChangesCommand()
        {
            IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
            if (ContactPhoto is not null)
            {
                using Stream dbFileStream = await dbService.CreateWriteStreamAsync(
                    innerContact.ImagePath = $"$/img/{innerContact.OtherPeer}.jpg"
                    );
                ContactPhoto.Save(dbFileStream);
            }

            innerContact.ChatColorCode = BubbleColor.ToUInt32();
            await dbService.InsertOrUpdateItemAsync(innerContact);

            IFrontendService frontendService = await serviceManager.GetWithAwaitAsync<IFrontendService>();
            if (!frontendService.NavigatePreviousView())
            {
                frontendService.NavigateContactView();
            }
        }

        public async Task EditCommand()
        {
            IFrontendService frontendService = await serviceManager.GetWithAwaitAsync<IFrontendService>();
            frontendService.NavigateContactView(innerContact);

            ShouldShowOptions = false;
        }

        public async Task DeleteCommand(bool deleteFromDb)
        {
            ILauncherService launcherService = await serviceManager.GetWithAwaitAsync<ILauncherService>();
            if (string.IsNullOrEmpty(TopicName) && await launcherService.ShowConfirmationDialogAsync("Remove this Contact?", deleteFromDb ?
                "Are you sure you want to remove this contact?" :
                "Are you sure you want to remove this recipient from the conversation?"))
            {
                if (deleteFromDb)
                {
                    IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
                    await dbService.DeleteItemByIdAsync<SubverseContact>(innerContact.Id);
                }

                contactContainer?.RemoveContact(this);
                ShouldShowOptions = false;
            }
            else if (!string.IsNullOrEmpty(TopicName) && await launcherService.ShowConfirmationDialogAsync(DELETE_CONFIRM_TITLE, DELETE_CONFIRM_MESSAGE))
            {
                IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();
                await dbService.DeleteAllMessagesOfTopicAsync(TopicName);

                if (deleteFromDb)
                {
                    await dbService.DeleteItemByIdAsync<SubverseContact>(innerContact.Id);
                }

                contactContainer?.RemoveContact(this);
                ShouldShowOptions = false;
            }
        }

        public async Task CopyCommand()
        {
            ILauncherService launcherService = await serviceManager.GetWithAwaitAsync<ILauncherService>();
            TopLevel topLevel = await serviceManager.GetWithAwaitAsync<TopLevel>();

            if (topLevel.Clipboard is not null)
            {
                await topLevel.Clipboard.SetTextAsync(
                    $"sv://{innerContact.OtherPeer}?name={HttpUtility.UrlEncode(innerContact.DisplayName)}"
                    );
                await launcherService.ShowAlertDialogAsync("Information", "Contact link copied to the clipboard.");
            }
            else
            {
                await launcherService.ShowAlertDialogAsync("Error", "Could not copy contact link to the clipboard.");
            }
        }

        public async Task CancelCommand()
        {
            IFrontendService frontendService = await serviceManager.GetWithAwaitAsync<IFrontendService>();
            if (!frontendService.NavigatePreviousView())
            {
                frontendService.NavigateContactView();
            }
        }
    }
}
