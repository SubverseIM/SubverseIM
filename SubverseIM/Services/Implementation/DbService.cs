using LiteDB;
using SubverseIM.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

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

        public IEnumerable<SubverseMessage> GetMessagesWithPeersOnTopic(IEnumerable<SubversePeerId> otherPeers, string? topicName)
        {
            var messages = db.GetCollection<SubverseMessage>();

            messages.EnsureIndex(x => x.Sender);
            messages.EnsureIndex(x => x.Recipient);

            messages.EnsureIndex(x => x.CallId, unique: true);

            return messages.Query()
                .Where(x => otherPeers.Contains(x.Sender) || otherPeers.Contains(x.Recipient))
                .Where(x => topicName == null || x.TopicName == topicName)
                .OrderByDescending(x => x.DateSignedOn)
                .ToEnumerable();
        }

        public bool InsertOrUpdateItem<T>(T item)
        {
            var contacts = db.GetCollection<T>();
            return contacts.Upsert(item);
        }

        public bool DeleteItemById<T>(BsonValue id)
        {
            var contacts = db.GetCollection<T>();
            return contacts.Delete(id);
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
