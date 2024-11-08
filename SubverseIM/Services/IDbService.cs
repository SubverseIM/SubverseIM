using LiteDB;
using SubverseIM.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace SubverseIM.Services
{
    public interface IDbService : IDisposable
    {
        IEnumerable<SubverseContact> GetContacts();

        SubverseContact GetContact(SubversePeerId otherPeer);

        IEnumerable<SubverseMessage> GetMessagesFromPeer(SubversePeerId otherPeer);

        bool InsertOrUpdateItem<T>(T item);

        bool DeleteItemById<T>(BsonValue id);

        bool TryGetReadStream(string path, [NotNullWhen(true)] out Stream? stream);

        Stream CreateWriteStream(string path);
    }
}
