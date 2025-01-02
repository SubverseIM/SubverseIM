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

        SubverseContact? GetContact(SubversePeerId otherPeer);

        IEnumerable<SubverseTorrent> GetFiles();

        IEnumerable<SubverseTorrent> GetFilesFromPeer(SubversePeerId ownerPeer);

        SubverseTorrent? GetFile(string magnetUri);

        IEnumerable<SubverseMessage> GetMessagesWithPeersOnTopic(HashSet<SubversePeerId> otherPeers, string? topicName);

        IEnumerable<SubverseMessage> GetAllUndeliveredMessages();

        IReadOnlyDictionary<string, IEnumerable<SubversePeerId>> GetAllMessageTopics();

        SubverseMessage? GetMessageByCallId(string callId);

        bool InsertOrUpdateItem(SubverseContact newItem);

        bool InsertOrUpdateItem(SubverseTorrent newItem);

        bool InsertOrUpdateItem(SubverseMessage newItem);

        bool DeleteItemById<T>(BsonValue id);

        void DeleteAllMessagesOfTopic(string topicName);

        bool TryGetReadStream(string path, [NotNullWhen(true)] out Stream? stream);

        Stream CreateWriteStream(string path);
    }
}
