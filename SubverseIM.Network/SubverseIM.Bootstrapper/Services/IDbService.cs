using SubverseIM.Bootstrapper.Models;

namespace SubverseIM.Bootstrapper.Services
{
    public interface IDbService : IDisposable
    {
        bool InsertMessage(SubverseMessage message);
    }
}
