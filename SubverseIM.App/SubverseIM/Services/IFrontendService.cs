using SubverseIM.Models;
using SubverseIM.ViewModels.Pages;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public interface IFrontendService : IRunnable, IBackgroundRunnable
    {
        Task ResetSizeAsync();

        Task RestorePurchasesAsync();

        Task NavigateLaunchedUriAsync(Uri? overrideUri = null);
    }
}
