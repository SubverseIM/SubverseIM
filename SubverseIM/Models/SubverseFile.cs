using LiteDB;

namespace SubverseIM.Models
{
    public class SubverseFile
    {
        public ObjectId? Id { get; set; }

        public string MagnetUri { get; set; }

        public SubversePeerId OwnerPeer { get; set; }

        public byte[]? TorrentBytes { get; set; }

        public SubverseFile(string magnetUri, SubversePeerId ownerPeer)
        {
            MagnetUri = magnetUri;
            OwnerPeer = ownerPeer;
        }
    }
}
