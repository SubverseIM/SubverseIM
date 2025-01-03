using MonoTorrent;
using System;

namespace SubverseIM.ViewModels.Components
{
    public class EmbedViewModel : ViewModelBase
    {
        private readonly Uri embeddedUri;

        public string HostName => MagnetLink.TryParse(embeddedUri.ToString(), 
            out MagnetLink? magnetLink) ? magnetLink.Name ?? "Untitled" : embeddedUri.Host;

        public string AbsoluteUri => embeddedUri.AbsoluteUri;

        public EmbedViewModel(string uriString) 
        {
            embeddedUri = new(uriString);
        }
    }
}
