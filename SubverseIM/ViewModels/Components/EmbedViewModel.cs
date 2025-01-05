using MonoTorrent;
using SubverseIM.Models;
using SubverseIM.Services;
using SubverseIM.Services.Implementation;
using System;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels.Components
{
    public class EmbedViewModel : ViewModelBase
    {
        private readonly IServiceManager serviceManager;

        private readonly string? embeddedStr;

        private readonly byte[]? embeddedBuf;

        public string Name { get; }

        public string? AbsoluteUri { get; }

        public EmbedViewModel(IServiceManager serviceManager, string embeddedStr) 
        {
            this.serviceManager = serviceManager;
            this.embeddedStr = embeddedStr;

            Name = (MagnetLink.TryParse(embeddedStr, out MagnetLink? magnetLink) ? 
                magnetLink.Name : new Uri(embeddedStr).Host) ?? "[untitled]";
            AbsoluteUri = embeddedStr;
        }

        public EmbedViewModel(IServiceManager serviceManager, byte[] embeddedBuf) 
        {
            this.serviceManager = serviceManager;
            this.embeddedBuf = embeddedBuf;

            Name = Torrent.TryLoad(embeddedBuf, out Torrent? torrent) ? 
                torrent.Name : "[untitled]";
            AbsoluteUri = null;
        }

        public async Task OpenCommandAsync() 
        {
            if (AbsoluteUri is null)
            {
                ITorrentService torrentService = await serviceManager.GetWithAwaitAsync<ITorrentService>();
                SubverseTorrent torrent = new SubverseTorrent
                {
                    MagnetUri = embeddedStr,
                    TorrentBytes = embeddedBuf,
                };
                await torrentService.AddTorrentAsync(torrent);

                IFrontendService frontendService = await serviceManager.GetWithAwaitAsync<IFrontendService>();
                frontendService.NavigateTorrentView();
            }
        }
    }
}
