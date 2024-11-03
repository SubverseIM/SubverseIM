using Avalonia;
using Avalonia.Media;
using SubverseIM.Services;
using System;
using System.Collections.Generic;

namespace SubverseIM.ViewModels.Pages
{
    public class ContactPageViewModel : PageViewModelBase
    {
        public IList<Point> HexagonPoints { get; }

        public Geometry HexagonPath { get; }

        private static IList<Point> GenerateHexagon(double r)
        {
            List<Point> points = new();
            for (int i = 0; i < 6; i++)
            {
                double theta = Math.Tau * i / 6.0;
                (double y, double x) = Math.SinCos(theta);
                points.Add(new((x + 1.0) * r, (y + 1.0) * r));
            }

            return points;
        }

        public ContactPageViewModel(IServiceManager serviceManager) : base(serviceManager)
        {
            HexagonPoints = GenerateHexagon(48);
            HexagonPath = new PolylineGeometry(GenerateHexagon(46), true);
        }
    }
}
