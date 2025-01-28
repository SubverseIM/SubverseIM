using System.Threading;
using System.Threading.Tasks;
using SubverseIM.Models;

namespace SubverseIM.Services.Implementation;

public class ConfigurationService : IConfigurationService, IInjectable
{
    private readonly TaskCompletionSource<SubverseConfig> configTcs;
    
    private readonly TaskCompletionSource<IServiceManager> serviceManagerTcs;

    public ConfigurationService()
    {
        configTcs = new();
        serviceManagerTcs = new();
    }

    #region IConfigurationService API

    public async Task<SubverseConfig> GetConfigAsync(CancellationToken cancellationToken)
    {
        return await configTcs.Task.WaitAsync(cancellationToken);
    }

    public async Task<bool> PersistConfigAsync(CancellationToken cancellationToken = default)
    {
        IServiceManager serviceManager = await serviceManagerTcs.Task;
        IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();

        SubverseConfig config = await GetConfigAsync(cancellationToken);
        dbService.UpdateConfig(config);

        return true;
    }

    #endregion

    #region IInjectable API

    public async Task InjectAsync(IServiceManager serviceManager)
    {
        IDbService dbService = await serviceManager.GetWithAwaitAsync<IDbService>();

        SubverseConfig config = dbService.GetConfig() ?? new SubverseConfig
        { BootstrapperUriList = [BootstrapperService.DEFAULT_BOOTSTRAPPER_ROOT] };
        configTcs.SetResult(config);
    }

    #endregion
}
