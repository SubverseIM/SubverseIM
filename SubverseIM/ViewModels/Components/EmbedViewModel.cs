using System;

namespace SubverseIM.ViewModels.Components
{
    public class EmbedViewModel : ViewModelBase
    {
        private readonly Uri embeddedUri;

        public string HostName => embeddedUri.Host;

        public string AbsoluteUri => embeddedUri.AbsoluteUri;

        public EmbedViewModel(string uriString) 
        {
            embeddedUri = new(uriString);
        }
    }
}
