using LiteDB;
using SubverseIM.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace SubverseIM.Services
{
    public interface IDbService : IDisposable
    {
        Stream GetStream(string path);

        IEnumerable<SubverseContact> GetContacts();

        IEnumerable<SubverseMessage> GetMessagesFromPeer(SubversePeerId otherPeer);

        bool InsertOrUpdateItem<T>(T item);

        bool DeleteItemById<T>(BsonValue id);
    }
}
