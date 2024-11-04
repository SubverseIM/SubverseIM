using LiteDB;
using SubverseIM.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SubverseIM.Services
{
    public class DbService : IDbService
    {
        private readonly LiteDatabase db;

        private bool disposedValue;

        public DbService(string dbConnectionString)
        {
            BsonMapper mapper = new(); 
            mapper.RegisterType<SubversePeerId>(
                serialize: (peerId) => peerId.ToString(),
                deserialize: (bson) => SubversePeerId.FromString(bson.AsString)
            );

            db = new(dbConnectionString, mapper);
        }

        public IEnumerable<SubverseContact> GetContacts()
        {
            var contacts = db.GetCollection<SubverseContact>();
            contacts.EnsureIndex(x => x.OtherPeer, unique: true);
            return contacts
                .FindAll()
                .OrderBy(x => x.DisplayName);
        }

        public IEnumerable<SubverseMessage> GetMessagesFromPeer(SubversePeerId otherPeer)
        {
            return db.GetCollection<SubverseMessage>()
                .Find(x => x.Sender == otherPeer)
                .OrderByDescending(x => x.DateSignedOn);
        }

        public Stream GetStream(string path)
        {
            return db.GetStorage<string>().OpenRead(path);
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
