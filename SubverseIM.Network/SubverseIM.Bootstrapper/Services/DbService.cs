using LiteDB;
using SubverseIM.Bootstrapper.Models;
using SubverseIM.Core;

namespace SubverseIM.Bootstrapper.Services
{
    public class DbService : IDbService
    {
        private readonly IConfiguration _configuration;

        private readonly LiteDatabase _dbContext;

        private bool _disposedValue;

        public DbService(IConfiguration configuration) 
        {
            _configuration = configuration;

            BsonMapper mapper = new();
            mapper.RegisterType(
                serialize: (peerId) => peerId.ToString(),
                deserialize: (bson) => SubversePeerId.FromString(bson.AsString)
            );

            _dbContext = new LiteDatabase(_configuration.GetConnectionString("LiteDb"), mapper);
        }

        public bool InsertMessage(SubverseMessage message)
        {
            var messages = _dbContext.GetCollection<SubverseMessage>();
            messages.EnsureIndex(x => x.MessageId, unique: true);
            try
            {
                messages.Insert(message);
                return true;
            }
            catch (LiteException ex) when (ex.ErrorCode == LiteException.INDEX_DUPLICATE_KEY) 
            {
                return false;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _dbContext.Dispose();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
