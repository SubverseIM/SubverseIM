using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface IBackgroundRunnable
    {
        Task RunOnceBackgroundAsync();
    }
}
