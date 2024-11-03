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
            db = new(dbConnectionString);
        }

        public IEnumerable<SubverseContact> GetContacts()
        {
            return db.GetCollection<SubverseContact>().FindAll()
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
