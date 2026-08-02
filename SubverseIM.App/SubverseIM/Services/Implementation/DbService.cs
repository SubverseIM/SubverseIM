using Avalonia.Controls;
using LiteDB;
using MonoTorrent;
using SubverseIM.Core;
using SubverseIM.Core.Storage.Messages;
using SubverseIM.Exceptions;
using SubverseIM.Models;
using SubverseIM.Serializers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services.Implementation
{
    public class DbService : IDbService
    {
        private class ExpressionWithParam : IEquatable<ExpressionWithParam>
        {
            public string Expression { get; }

            public BsonValue? Param { get; }

            public ExpressionWithParam(string expression, BsonValue? param)
            {
                Expression = expression;
                Param = param;
            }

            public bool Equals(ExpressionWithParam? other)
            {
                return Expression == other?.Expression && Param == other?.Param;
            }

            public override bool Equals(object? obj)
            {
                return Equals(obj as ExpressionWithParam);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(Expression, Param);
            }

            public static bool operator ==(ExpressionWithParam a, ExpressionWithParam b)
            {
                return a.Equals(b);
            }

            public static bool operator !=(ExpressionWithParam a, ExpressionWithParam b)
            {
                return !(a == b);
            }
        }

        private readonly string dbFilePath;

        private readonly BsonMapper mapper;

        private readonly TaskCompletionSource<LiteDatabase> dbTcs;

        private readonly Dictionary<ExpressionWithParam, BsonExpression> exprCache;

        private bool disposedValue;

        public bool UseSeparateThread => false;

        public DbService(string dbFilePath)
        {
            this.dbFilePath = dbFilePath;

            BsonMapper mapper = new();
            mapper.RegisterType(
                serialize: (peerId) => peerId.ToString(),
                deserialize: (bson) => SubversePeerId.FromString(bson.AsString)
                );
            mapper.RegisterType(
                serialize: (infoHash) => infoHash.ToHex(),
                deserialize: (bson) => InfoHash.FromHex(bson.AsString)
                );
            this.mapper = mapper;

            exprCache = new();

            dbTcs = new();
        }

        private BsonExpression GetOrCreateExpression(string expression, BsonValue param)
        {
            lock (exprCache)
            {
                ExpressionWithParam expressionWithParam = new(expression, param);
                if (!exprCache.TryGetValue(expressionWithParam, out BsonExpression? expr))
                {
                    expr = BsonExpression.Create(expression, param);
                    exprCache.Add(expressionWithParam, expr);
                }
                return expr;
            }
        }

        private BsonExpression GetOrCreateExpression(string expression)
        {
            lock (exprCache)
            {
                ExpressionWithParam expressionWithParam = new(expression, null);
                if (!exprCache.TryGetValue(expressionWithParam, out BsonExpression? expr))
                {
                    expr = BsonExpression.Create(expression);
                    exprCache.Add(expressionWithParam, expr);
                }
                return expr;
            }
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
            contacts.EnsureIndex("OtherPeer", GetOrCreateExpression("OtherPeer"), unique: true);
            return contacts.Query()
                .OrderByDescending(GetOrCreateExpression("DateLastChattedWith"))
                .ToEnumerable();
        }

        public async Task<SubverseContact?> GetContactAsync(SubversePeerId otherPeer, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LiteDatabase db = await dbTcs.Task.WaitAsync(cancellationToken);

            var contacts = db.GetCollection<SubverseContact>();
            contacts.EnsureIndex("OtherPeer", GetOrCreateExpression("OtherPeer"), unique: true);
            return contacts.FindOne(GetOrCreateExpression("OtherPeer = {0}", mapper.Serialize(otherPeer)));
        }

        public async Task<IEnumerable<SubverseTorrent>> GetTorrentsAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LiteDatabase db = await dbTcs.Task.WaitAsync(cancellationToken);

            var torrents = db.GetCollection<SubverseTorrent>();
            torrents.EnsureIndex("MagnetUri", GetOrCreateExpression("MagnetUri"), unique: true);

            return torrents.Query()
                .OrderByDescending(GetOrCreateExpression("DateLastUpdatedOn"))
                .ToEnumerable();
        }

        public async Task<SubverseTorrent?> GetTorrentAsync(InfoHash infoHash, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LiteDatabase db = await dbTcs.Task.WaitAsync(cancellationToken);

            var torrents = db.GetCollection<SubverseTorrent>();
            torrents.EnsureIndex("InfoHash", GetOrCreateExpression("InfoHash"), unique: true);

            return torrents.FindOne(GetOrCreateExpression("InfoHash = {0}", mapper.Serialize(infoHash)));
        }

        public async Task<IEnumerable<SubverseMessage>> GetMessagesWithPeersOnTopicAsync(HashSet<SubversePeerId> otherPeers, string topicName, bool orderFlag, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LiteDatabase db = await dbTcs.Task.WaitAsync(cancellationToken);

            var messages = db.GetCollection<SubverseMessage>();

            messages.EnsureIndex("Sender", GetOrCreateExpression("Sender"));
            messages.EnsureIndex("Recipients", GetOrCreateExpression("Recipients"));
            messages.EnsureIndex("TopicName", GetOrCreateExpression("TopicName"));
            messages.EnsureIndex("MessageId", GetOrCreateExpression("MessageId"), unique: true);

            IEnumerable<SubverseMessage> topicMessages = otherPeers
                .SelectMany(otherPeer => messages.Query()
                    .Where(GetOrCreateExpression("WasDecrypted != false"))
                    .Where(GetOrCreateExpression("Sender = {0} OR CONTAINS(Recipients, {0})", mapper.Serialize(otherPeer)))
                    .GroupBy(GetOrCreateExpression("TopicName"))
                    .Having(GetOrCreateExpression("LENGTH(@key) = 0 OR @key = {0}", topicName))
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

            messages.EnsureIndex("Sender", GetOrCreateExpression("Sender"));
            messages.EnsureIndex("Recipients", GetOrCreateExpression("Recipients"));
            messages.EnsureIndex("TopicName", GetOrCreateExpression("TopicName"));
            messages.EnsureIndex("MessageId", GetOrCreateExpression("MessageId"), unique: true);

            return messages.Query()
                .Where(GetOrCreateExpression("WasDelivered = false"))
                .OrderByDescending(GetOrCreateExpression("DateSignedOn"))
                .ToEnumerable();
        }

        public async Task<IReadOnlyDictionary<string, IEnumerable<SubversePeerId>>> GetAllMessageTopicsAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LiteDatabase db = await dbTcs.Task.WaitAsync(cancellationToken);

            var messages = db.GetCollection<SubverseMessage>();

            messages.EnsureIndex("Sender", GetOrCreateExpression("Sender"));
            messages.EnsureIndex("Recipients", GetOrCreateExpression("Recipients"));
            messages.EnsureIndex("TopicName", GetOrCreateExpression("TopicName"));
            messages.EnsureIndex("MessageId", GetOrCreateExpression("MessageId"), unique: true);

            return messages.Query()
                .Where(GetOrCreateExpression("LENGTH(TopicName) > 0 AND TopicName != '#system'"))
                .GroupBy(GetOrCreateExpression("TopicName"))
                .Select(GetOrCreateExpression("{ Key: @key, Participants: DISTINCT(CONCAT(*.Recipients[*], *.Sender)) }"))
                .ToEnumerable()
                .ToDictionary(g => g["Key"].AsString, g => g["Participants"]
                .AsArray.Select(mapper.Deserialize<SubversePeerId>));
        }

        public async Task<SubverseMessage?> GetMessageByIdAsync(MessageId messageId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LiteDatabase db = await dbTcs.Task.WaitAsync(cancellationToken);

            var messages = db.GetCollection<SubverseMessage>();

            messages.EnsureIndex("Sender", GetOrCreateExpression("Sender"));
            messages.EnsureIndex("Recipients", GetOrCreateExpression("Recipients"));
            messages.EnsureIndex("TopicName", GetOrCreateExpression("TopicName"));
            messages.EnsureIndex("MessageId", GetOrCreateExpression("MessageId"), unique: true);

            return messages.FindOne(GetOrCreateExpression("MessageId = {0}", mapper.Serialize(messageId)));
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

            SubverseTorrent? storedItem = await GetTorrentAsync(newItem.InfoHash, cancellationToken);
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

            messages.EnsureIndex("Sender", GetOrCreateExpression("Sender"));
            messages.EnsureIndex("Recipients", GetOrCreateExpression("Recipients"));
            messages.EnsureIndex("TopicName", GetOrCreateExpression("TopicName"));
            messages.EnsureIndex("MessageId", GetOrCreateExpression("MessageId"), unique: true);

            messages.DeleteMany(GetOrCreateExpression("TopicName = {0}", topicName));
        }

        public async Task WriteAllMessagesOfTopicAsync(ISerializer<SubverseMessage> serializer, string topicName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LiteDatabase db = await dbTcs.Task.WaitAsync(cancellationToken);

            var messages = db.GetCollection<SubverseMessage>();

            messages.EnsureIndex("Sender", GetOrCreateExpression("Sender"));
            messages.EnsureIndex("Recipients", GetOrCreateExpression("Recipients"));
            messages.EnsureIndex("TopicName", GetOrCreateExpression("TopicName"));
            messages.EnsureIndex("MessageId", GetOrCreateExpression("MessageId"), unique: true);

            foreach (SubverseMessage message in messages.Query()
                .Where(GetOrCreateExpression("TopicName = {0}", topicName))
                .OrderByDescending(GetOrCreateExpression("DateSignedOn"))
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
            // Ensure that UI is initialized first
            _ = await serviceManager.GetWithAwaitAsync<TopLevel>();

            try
            {
                IEncryptionService encryptionService = await serviceManager.GetWithAwaitAsync<IEncryptionService>();
                string? dbPassword = await encryptionService.GetEncryptionKeyAsync();
                dbTcs.SetResult(new LiteDatabase(new ConnectionString
                {
                    Filename = dbFilePath,
                    Password = dbPassword,
                }, mapper));
            }
            catch (EncryptionServiceException)
            {
                dbTcs.SetException(new DbServiceException("Could not decrypt the application database, possibly because the user denied authentication."));
            }
            catch (Exception ex)
            {
                dbTcs.SetException(ex);
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
