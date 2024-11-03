using SubverseIM.Models;
using System.Collections.Generic;
using System.IO;

namespace SubverseIM.Services
{
    public interface IDbService
    {
        Stream GetStream(string path);

        IEnumerable<SubverseContact> GetContacts();

        IEnumerable<SubverseMessage> GetMessagesFromPeer(SubversePeerId otherPeer);
    }
}
