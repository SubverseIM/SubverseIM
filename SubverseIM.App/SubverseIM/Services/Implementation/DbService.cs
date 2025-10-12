using LiteDB;
using SubverseIM.Core;
using SubverseIM.Exceptions;
using SubverseIM.Models;
using SubverseIM.Serializers;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services.Implementation
{
    public class DbService : IDbService
    {
        private readonly string dbFilePath;

        private readonly BsonMapper mapper;

        private readonly TaskCompletionSource<LiteDatabase> dbTcs;

        private bool disposedValue;

        public DbService(string dbFilePath)
        {
            this.dbFilePath = dbFilePath;

            BsonMapper mapper = new();
            mapper.RegisterType(
                serialize: (peerId) => peerId.ToString(),
                deserialize: (bson) => SubversePeerId.FromString(bson.AsString)
            );
            this.mapper = mapper;

            dbTcs = new();
        }

        public async Task<SubverseConfig?> GetConfigAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LiteDatabase db = await dbTcs.Task.WaitAsync(cancellationToken);

            return db.GetCollection<SubverseConfig>()
                .FindAll().SingleOrDefault();
        }

        public async Task<bool> UpdateConfigAsync(SubverseConfig config, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LiteDatabase db = await dbTcs.Task.WaitAsync(cancellationToken);

            return db.GetCollection<SubverseConfig>().Upsert(config);
        }

        public async Task<IEnumerable<SubverseContact>> GetContactsAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LiteDatabase db = await dbTcs.Task.WaitAsync(cancellationToken);

            var contacts = db.GetCollection<SubverseContact>();
            contacts.EnsureIndex(x => x.OtherPeer, unique: true);
            return contacts.Query()
                .OrderByDescending(x => x.DateLastChattedWith)
                .ToEnumerable();
        }

        public async Task<SubverseContact?> GetContactAsync(SubversePeerId otherPeer, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LiteDatabase db = await dbTcs.Task.WaitAsync(cancellationToken);

            var contacts = db.GetCollection<SubverseContact>();
            contacts.EnsureIndex(x => x.OtherPeer, unique: true);
            return contacts.FindOne(x => x.OtherPeer == otherPeer);
        }

        public async Task<IEnumerable<SubverseTorrent>> GetTorrentsAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LiteDatabase db = await dbTcs.Task.WaitAsync(cancellationToken);

            var torrents = db.GetCollection<SubverseTorrent>();
            torrents.EnsureIndex(x => x.MagnetUri, unique: true);

            return torrents.Query()
                .OrderByDescending(x => x.DateLastUpdatedOn)
                .ToEnumerable();
        }

        public async Task<SubverseTorrent?> GetTorrentAsync(string magnetUri, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LiteDatabase db = await dbTcs.Task.WaitAsync(cancellationToken);

            var torrents = db.GetCollection<SubverseTorrent>();
            torrents.EnsureIndex(x => x.MagnetUri, unique: true);

            return torrents.FindOne(x => x.MagnetUri == magnetUri);
        }

        public async Task<IEnumerable<SubverseMessage>> GetMessagesWithPeersOnTopicAsync(HashSet<SubversePeerId> otherPeers, string? topicName, bool orderFlag, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LiteDatabase db = await dbTcs.Task.WaitAsync(cancellationToken);

            var messages = db.GetCollection<SubverseMessage>();

            messages.EnsureIndex(x => x.Sender);
            messages.EnsureIndex(x => x.Recipients);

            messages.EnsureIndex(x => x.MessageId, unique: true);

            IEnumerable<SubverseMessage> topicMessages = otherPeers
                .SelectMany(otherPeer => messages.Query()
                .Where(x => x.WasDecrypted ?? true)
                .Where(x => otherPeer == x.Sender || ((IEnumerable<SubversePeerId>)x.Recipients).Contains(otherPeer))
                .Where(x => string.IsNullOrEmpty(topicName) || x.TopicName == topicName)
                .ToEnumerable())
                .DistinctBy(x => x.MessageId);

            return orderFlag ? topicMessages.OrderBy(x => x.DateSignedOn) :
                topicMessages.OrderByDescending(x => x.DateSignedOn);
        }

        public async Task<IEnumerable<SubverseMessage>> GetAllUndeliveredMessagesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LiteDatabase db = await dbTcs.Task.WaitAsync(cancellationToken);

            var messages = db.GetCollection<SubverseMessage>();

            messages.EnsureIndex(x => x.Sender);
            messages.EnsureIndex(x => x.Recipients);

            messages.EnsureIndex(x => x.MessageId, unique: true);

            return messages.Query()
                .Where(x => !x.WasDelivered)
                .OrderByDescending(x => x.DateSignedOn)
                .ToEnumerable();
        }

        public async Task<IReadOnlyDictionary<string, IEnumerable<SubversePeerId>>> GetAllMessageTopicsAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LiteDatabase db = await dbTcs.Task.WaitAsync(cancellationToken);

            var messages = db.GetCollection<SubverseMessage>();

            messages.EnsureIndex(x => x.Sender);
            messages.EnsureIndex(x => x.Recipients);

            messages.EnsureIndex(x => x.MessageId, unique: true);

            return messages.Query()
                .OrderByDescending(x => x.DateSignedOn)
                .Where(x => !string.IsNullOrEmpty(x.TopicName) && x.TopicName != "#system")
                .ToEnumerable()
                .GroupBy(x => x.TopicName!)
                .ToFrozenDictionary(g => g.Key, g => g
                    .SelectMany(x => x.Recipients)
                    .Distinct());
        }

        public async Task<SubverseMessage?> GetMessageByIdAsync(MessageId messageId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LiteDatabase db = await dbTcs.Task.WaitAsync(cancellationToken);

            var messages = db.GetCollection<SubverseMessage>();

            messages.EnsureIndex(x => x.Sender);
            messages.EnsureIndex(x => x.Recipients);

            messages.EnsureIndex(x => x.MessageId, unique: true);

            return messages.FindOne(x => x.MessageId == messageId);
        }

        public async Task<bool> InsertOrUpdateItemAsync(SubverseContact newItem, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LiteDatabase db = await dbTcs.Task.WaitAsync(cancellationToken);

            var contacts = db.GetCollection<SubverseContact>();

            SubverseContact? storedItem = await GetContactAsync(newItem.OtherPeer, cancellationToken);
            newItem.Id = storedItem?.Id;

            return contacts.Upsert(newItem);
        }

        public async Task<bool> InsertOrUpdateItemAsync(SubverseTorrent newItem, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LiteDatabase db = await dbTcs.Task.WaitAsync(cancellationToken);

            var torrents = db.GetCollection<SubverseTorrent>();

            SubverseTorrent? storedItem = await GetTorrentAsync(newItem.MagnetUri, cancellationToken);
            newItem.Id = storedItem?.Id;

            return torrents.Upsert(newItem);
        }

        public async Task<bool> InsertOrUpdateItemAsync(SubverseMessage newItem, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LiteDatabase db = await dbTcs.Task.WaitAsync(cancellationToken);

            var messages = db.GetCollection<SubverseMessage>();
            return messages.Upsert(newItem);
        }

        public async Task<bool> DeleteItemByIdAsync<T>(BsonValue id, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LiteDatabase db = await dbTcs.Task.WaitAsync(cancellationToken);

            var collection = db.GetCollection<T>();
            return collection.Delete(id);
        }

        public async Task DeleteAllMessagesOfTopicAsync(string topicName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LiteDatabase db = await dbTcs.Task.WaitAsync(cancellationToken);

            var messages = db.GetCollection<SubverseMessage>();

            messages.EnsureIndex(x => x.Sender);
            messages.EnsureIndex(x => x.Recipients);

            messages.EnsureIndex(x => x.MessageId, unique: true);

            messages.DeleteMany(x => x.TopicName == topicName);
        }

        public async Task WriteAllMessagesOfTopicAsync(ISerializer<SubverseMessage> serializer, string topicName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LiteDatabase db = await dbTcs.Task.WaitAsync(cancellationToken);

            var messages = db.GetCollection<SubverseMessage>();

            messages.EnsureIndex(x => x.Sender);
            messages.EnsureIndex(x => x.Recipients);

            messages.EnsureIndex(x => x.MessageId, unique: true);

            foreach (SubverseMessage message in messages.Query()
                .Where(x => x.TopicName == topicName)
                .OrderByDescending(x => x.DateSignedOn)
                .ToEnumerable())
            {
                serializer.Serialize(message);
            }
        }

        public async Task<Stream?> GetReadStreamAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LiteDatabase db = await dbTcs.Task.WaitAsync(cancellationToken);

            if (db.GetStorage<string>().Exists(path))
            {
                return db.GetStorage<string>().OpenRead(path);
            }
            else
            {
                return null;
            }
        }

        public async Task<Stream> CreateWriteStreamAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LiteDatabase db = await dbTcs.Task.WaitAsync(cancellationToken);

            return db.GetStorage<string>().OpenWrite(path, Path.GetFileName(path));
        }

        public async Task InjectAsync(IServiceManager serviceManager)
        {
            IEncryptionService? encryptionService = serviceManager.Get<IEncryptionService>();
            string? dbPassword;
            try
            {
                if (encryptionService is null)
                {
                    dbPassword = IDbService.SECRET_PASSWORD;
                }
                else
                {
                    dbPassword = await encryptionService.GetEncryptionKeyAsync();
                }
            }
            catch
            {
                dbPassword = null;
            }

            if (dbPassword is null)
            {
                dbTcs.SetException(new DbServiceException("Could not decrypt the application database, possibly because the user denied authentication."));
            }
            else
            {
                dbTcs.SetResult(new LiteDatabase(new ConnectionString
                {
                    Filename = dbFilePath,
                    Password = dbPassword,
                }, mapper));
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (dbTcs.Task.IsCompletedSuccessfully)
                    {
                        dbTcs.Task.Result.Dispose();
                    }
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
