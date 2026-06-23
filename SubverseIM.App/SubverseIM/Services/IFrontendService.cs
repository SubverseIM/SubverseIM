using SubverseIM.Models;
using System;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface IFrontendService : IRunnable, IBackgroundRunnable
    {
        Task ResetSizeAsync();

        Task RestorePurchasesAsync();
    }
}
