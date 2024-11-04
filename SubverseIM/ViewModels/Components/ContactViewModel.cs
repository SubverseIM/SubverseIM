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

        public IList<Point> HexagonPoints => hexagonPoints;

        public Geometry HexagonPath => hexagonPath;

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
                    new Uri("avares://SubverseIM/Assets/izzy.jpg")
                    );
            }
            else 
            {
                contactPhotoStream = dbService.GetStream(innerContact.ImagePath);
            }

            ContactPhoto = Bitmap.DecodeToWidth(contactPhotoStream, 64);
        }
    }
}
