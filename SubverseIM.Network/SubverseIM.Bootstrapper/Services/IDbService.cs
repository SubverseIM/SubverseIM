using SubverseIM.Bootstrapper.Models;

namespace SubverseIM.Bootstrapper.Services
{
    public interface IDbService : IDisposable
    {
        bool InsertMessage(SubverseMessage message);

        bool InsertFriend(SubverseFriend friend);

        IEnumerable<SubverseFriend> GetRecentFriends(int? maxListCount = null, TimeSpan? expireTime = null);
    }
}
