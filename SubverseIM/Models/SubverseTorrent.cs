using LiteDB;

namespace SubverseIM.Models
{
    public class SubverseTorrent
    {
        public ObjectId? Id { get; set; }

        public string MagnetUri { get; set; }

        public byte[]? TorrentBytes { get; set; }

        public SubverseTorrent(string magnetUri) 
        {
            MagnetUri = magnetUri;
        }
    }
}
