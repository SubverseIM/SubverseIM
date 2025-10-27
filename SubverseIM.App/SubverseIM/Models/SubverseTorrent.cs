using LiteDB;
using MonoTorrent;
using System;

namespace SubverseIM.Models
{
    public class SubverseTorrent
    {
        public ObjectId? Id { get; set; }
        
        public InfoHash InfoHash { get; set; }

        public string MagnetUri { get; set; }

        public byte[]? TorrentBytes { get; set; }

        public DateTime DateLastUpdatedOn { get; set; }

        public SubverseTorrent(InfoHash infoHash, string magnetUri) 
        {
            InfoHash = infoHash;
            MagnetUri = magnetUri;
        }
    }
}
