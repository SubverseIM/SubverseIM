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

            mapper.RegisterType(
                serialize: (uri) => uri.OriginalString,
                deserialize: (bson) => new Uri(bson.AsString)
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

        public bool InsertFriend(SubverseFriend friend)
        {
            var friends = _dbContext.GetCollection<SubverseFriend>();
            friends.EnsureIndex(x => x.Address, unique: true);

            SubverseFriend? oldFriend = friends.FindOne(x => x.Address == friend.Address);
            friend.Id = oldFriend.Id;

            return friends.Upsert(friend);
        }

        public IEnumerable<SubverseFriend> GetRecentFriends(int? maxListCount, TimeSpan? expireTime)
        {
            var now = DateTime.UtcNow;
            var friends = _dbContext.GetCollection<SubverseFriend>();

            ILiteQueryable<SubverseFriend> query;
            if (expireTime.HasValue)
            {
                query = friends.Query()
                    .Where(x => now - x.LastSeenOn < expireTime)
                    .OrderByDescending(x => x.LastSeenOn);
            }
            else
            {
                query = friends.Query()
                    .OrderByDescending(x => x.LastSeenOn);
            }

            if (maxListCount.HasValue)
            {
                return query.Limit(maxListCount.Value).ToEnumerable();
            }
            else
            {
                return query.ToEnumerable();
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
