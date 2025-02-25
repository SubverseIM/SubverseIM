using PgpCore;
using SubverseIM.Core;
using System.Collections.Generic;
using System.Net;

namespace SubverseIM.Models
{
    public class SubversePeer
    {
        public SubversePeerId OtherPeer { get; set; }

        public HashSet<IPEndPoint> RemoteEndPoints { get; set; }

        public EncryptionKeys? KeyContainer { get; set; }

        public SubversePeer() 
        {
            RemoteEndPoints = new();
        }
    }
}
