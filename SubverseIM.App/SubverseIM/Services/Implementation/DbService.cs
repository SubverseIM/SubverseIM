using LiteDB;
using SubverseIM.Core;
using SubverseIM.Models;
using SubverseIM.Serializers;
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace SubverseIM.Services.Implementation
{
    public class DbService : IDbService
    {
        private readonly LiteDatabase db;

        private bool disposedValue;

        public DbService(string dbConnectionString)
        {
            BsonMapper mapper = new();
            mapper.RegisterType(
                serialize: (peerId) => peerId.ToString(),
                deserialize: (bson) => SubversePeerId.FromString(bson.AsString)
            );

            db = new(dbConnectionString, mapper);
        }

        public SubverseConfig? GetConfig()
        {
            return db.GetCollection<SubverseConfig>()
                .FindAll().SingleOrDefault();
        }

        public bool UpdateConfig(SubverseConfig config) 
        {
            return db.GetCollection<SubverseConfig>().Upsert(config);
        }

        public IEnumerable<SubverseContact> GetContacts()
        {
            var contacts = db.GetCollection<SubverseContact>();
            contacts.EnsureIndex(x => x.OtherPeer, unique: true);
            return contacts.Query()
                .OrderByDescending(x => x.DateLastChattedWith)
                .ToEnumerable();
        }

        public SubverseContact? GetContact(SubversePeerId otherPeer)
        {
            var contacts = db.GetCollection<SubverseContact>();
            contacts.EnsureIndex(x => x.OtherPeer, unique: true);
            return contacts.FindOne(x => x.OtherPeer == otherPeer);
        }

        public IEnumerable<SubverseTorrent> GetTorrents()
        {
            var torrents = db.GetCollection<SubverseTorrent>();
            torrents.EnsureIndex(x => x.MagnetUri, unique: true);

            return torrents.Query()
                .OrderByDescending(x => x.DateLastUpdatedOn)
                .ToEnumerable();
        }

        public SubverseTorrent? GetTorrent(string magnetUri)
        {
            var torrents = db.GetCollection<SubverseTorrent>();
            torrents.EnsureIndex(x => x.MagnetUri, unique: true);

            return torrents.FindOne(x => x.MagnetUri == magnetUri);
        }

        public IEnumerable<SubverseMessage> GetMessagesWithPeersOnTopic(HashSet<SubversePeerId> otherPeers, string? topicName)
        {
            var messages = db.GetCollection<SubverseMessage>();

            messages.EnsureIndex(x => x.Sender);
            messages.EnsureIndex(x => x.Recipients);

            messages.EnsureIndex(x => x.CallId, unique: true);

            return otherPeers.SelectMany(otherPeer => messages.Query()
                .Where(x => x.WasDecrypted ?? true)
                .Where(x => otherPeer == x.Sender || x.Recipients.Contains(otherPeer))
                .Where(x => string.IsNullOrEmpty(topicName) || x.TopicName == topicName)
                .ToEnumerable())
                .DistinctBy(x => x.CallId)
                .OrderByDescending(x => x.DateSignedOn);
        }

        public IEnumerable<SubverseMessage> GetAllUndeliveredMessages()
        {
            var messages = db.GetCollection<SubverseMessage>();

            messages.EnsureIndex(x => x.Sender);
            messages.EnsureIndex(x => x.Recipients);

            messages.EnsureIndex(x => x.CallId, unique: true);

            return messages.Query()
                .Where(x => !x.WasDelivered)
                .OrderBy(x => x.DateSignedOn)
                .ToEnumerable();
        }

        public IReadOnlyDictionary<string, IEnumerable<SubversePeerId>> GetAllMessageTopics()
        {
            var messages = db.GetCollection<SubverseMessage>();

            messages.EnsureIndex(x => x.Sender);
            messages.EnsureIndex(x => x.Recipients);

            messages.EnsureIndex(x => x.CallId, unique: true);

            return messages.Query()
                .OrderByDescending(x => x.DateSignedOn)
                .Where(x => !string.IsNullOrEmpty(x.TopicName) && x.TopicName != "#system")
                .ToEnumerable()
                .GroupBy(x => x.TopicName!)
                .ToFrozenDictionary(g => g.Key, g => g
                    .SelectMany(x => x.Recipients)
                    .Distinct());
        }

        public SubverseMessage? GetMessageByCallId(string callId)
        {
            var messages = db.GetCollection<SubverseMessage>();

            messages.EnsureIndex(x => x.Sender);
            messages.EnsureIndex(x => x.Recipients);

            messages.EnsureIndex(x => x.CallId, unique: true);

            return messages.FindOne(x => x.CallId == callId);
        }

        public bool InsertOrUpdateItem(SubverseContact newItem)
        {
            var contacts = db.GetCollection<SubverseContact>();

            SubverseContact? storedItem = GetContact(newItem.OtherPeer);
            newItem.Id = storedItem?.Id;

            return contacts.Upsert(newItem);
        }

        public bool InsertOrUpdateItem(SubverseTorrent newItem)
        {
            var torrents = db.GetCollection<SubverseTorrent>();

            SubverseTorrent? storedItem = GetTorrent(newItem.MagnetUri);
            newItem.Id = storedItem?.Id;

            return torrents.Upsert(newItem);
        }

        public bool InsertOrUpdateItem(SubverseMessage newItem)
        {
            var messages = db.GetCollection<SubverseMessage>();
            return messages.Upsert(newItem);
        }

        public bool DeleteItemById<T>(BsonValue id)
        {
            var collection = db.GetCollection<T>();
            return collection.Delete(id);
        }

        public void DeleteAllMessagesOfTopic(string topicName)
        {
            var messages = db.GetCollection<SubverseMessage>();

            messages.EnsureIndex(x => x.Sender);
            messages.EnsureIndex(x => x.Recipients);

            messages.EnsureIndex(x => x.CallId, unique: true);

            messages.DeleteMany(x => x.TopicName == topicName);
        }

        public void WriteAllMessagesOfTopic(ISerializer<SubverseMessage> serializer, string topicName)
        {
            var messages = db.GetCollection<SubverseMessage>();

            messages.EnsureIndex(x => x.Sender);
            messages.EnsureIndex(x => x.Recipients);

            messages.EnsureIndex(x => x.CallId, unique: true);

            foreach (SubverseMessage message in messages.Query()
                .Where(x => x.TopicName == topicName)
                .OrderByDescending(x => x.DateSignedOn)
                .ToEnumerable()) 
            {
                serializer.Serialize(message);
            }
        }

        public bool TryGetReadStream(string path, [NotNullWhen(true)] out Stream? stream)
        {
            if (db.GetStorage<string>().Exists(path))
            {
                stream = db.GetStorage<string>().OpenRead(path);
                return true;
            }
            else
            {
                stream = null;
                return false;
            }
        }

        public Stream CreateWriteStream(string path)
        {
            return db.GetStorage<string>().OpenWrite(path, Path.GetFileName(path));
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    db.Dispose();
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
