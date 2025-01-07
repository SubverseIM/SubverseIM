using MonoTorrent;
using System;

namespace SubverseIM.ViewModels.Components
{
    public class EmbedViewModel : ViewModelBase
    {
        public string AbsoluteUri { get; }

        public string Name => MagnetLink.TryParse(AbsoluteUri,
            out MagnetLink? magnetLink) ? magnetLink.Name ?? "Untitled" :
            new Uri(AbsoluteUri).Host;

        public EmbedViewModel(string uriString)
        {
            AbsoluteUri = uriString;
        }
    }
}
