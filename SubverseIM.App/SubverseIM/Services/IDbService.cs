using LiteDB;
using SubverseIM.Core;
using SubverseIM.Models;
using SubverseIM.Serializers;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface IDbService : IInjectable, IDisposableService
    {
        public const string SECRET_PASSWORD = "#FreeTheInternet";

        public const string PUBLIC_KEY_PATH = "$/pkx/public.key";

        public const string PRIVATE_KEY_PATH = "$/pkx/private.key";

        public const string NODES_LIST_PATH = "$/pkx/nodes.list";

        Task<SubverseConfig?> GetConfigAsync(CancellationToken cancellationToken = default);

        Task<bool> UpdateConfigAsync(SubverseConfig config, CancellationToken cancellationToken = default);

        Task<IEnumerable<SubverseContact>> GetContactsAsync(CancellationToken cancellationToken = default);

        Task<SubverseContact?> GetContactAsync(SubversePeerId otherPeer, CancellationToken cancellationToken = default);

        Task<IEnumerable<SubverseTorrent>> GetTorrentsAsync(CancellationToken cancellationToken = default);

        Task<SubverseTorrent?> GetTorrentAsync(string magnetUri, CancellationToken cancellationToken = default);

        Task<IEnumerable<SubverseMessage>> GetMessagesWithPeersOnTopicAsync(HashSet<SubversePeerId> otherPeers, string? topicName = null, bool orderFlag = false, CancellationToken cancellationToken = default);

        Task<IEnumerable<SubverseMessage>> GetAllUndeliveredMessagesAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyDictionary<string, IEnumerable<SubversePeerId>>> GetAllMessageTopicsAsync(CancellationToken cancellationToken = default);

        Task<SubverseMessage?> GetMessageByIdAsync(MessageId messageId, CancellationToken cancellationToken = default);

        Task<bool> InsertOrUpdateItemAsync(SubverseContact newItem, CancellationToken cancellationToken = default);

        Task<bool> InsertOrUpdateItemAsync(SubverseTorrent newItem, CancellationToken cancellationToken = default);

        Task<bool> InsertOrUpdateItemAsync(SubverseMessage newItem, CancellationToken cancellationToken = default);

        Task<bool> DeleteItemByIdAsync<T>(BsonValue id, CancellationToken cancellationToken = default);

        Task DeleteAllMessagesOfTopicAsync(string topicName, CancellationToken cancellationToken = default);

        Task WriteAllMessagesOfTopicAsync(ISerializer<SubverseMessage> serializer, string topicName, CancellationToken cancellationToken = default);

        Task<Stream?> GetReadStreamAsync(string path, CancellationToken cancellationToken = default);

        Task<Stream> CreateWriteStreamAsync(string path, CancellationToken cancellationToken = default);
    }
}
