using PgpCore;
using SubverseIM.Core;
using System.Net;

namespace SubverseIM.Models
{
    public class SubversePeer
    {
        public SubversePeerId OtherPeer { get; set; }

        public IPEndPoint? RemoteEndPoint { get; set; }

        public EncryptionKeys? KeyContainer { get; set; }
    }
}
