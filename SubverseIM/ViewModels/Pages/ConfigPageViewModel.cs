using SubverseIM.Models;
using SubverseIM.Services;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels.Pages
{
    public class ConfigPageViewModel : PageViewModelBase<ConfigPageViewModel>
    {
        public override bool HasSidebar => false;

        public override string Title => "Configuration View";

        public ObservableCollection<string> BootstrapperUriList { get; }

        public ObservableCollection<string> SelectedUriList { get; }

        public ConfigPageViewModel(IServiceManager serviceManager) : base(serviceManager)
        {
            BootstrapperUriList = new();
            SelectedUriList = new();
        }

        public async Task InitializeAsync()
        {
            IPeerService peerService = await ServiceManager.GetWithAwaitAsync<IPeerService>();
            SubverseConfig config = await peerService.GetConfigAsync();

            BootstrapperUriList.Clear();
            foreach (string bootstrapperUri in config.BootstrapperUriList ?? [])
            {
                BootstrapperUriList.Add(bootstrapperUri);
            }
        }

        public async Task AddBootstrapperUriCommand()
        {
            ILauncherService launcherService = await ServiceManager.GetWithAwaitAsync<ILauncherService>();
            string? newBootstrapperUri = await launcherService.ShowInputDialogAsync("New Bootstrapper URI", "https://subverse.network/");
            if (newBootstrapperUri is not null)
            {
                BootstrapperUriList.Add(newBootstrapperUri);
            }
        }

        public void RemoveBootstrapperUriCommand()
        {
            foreach (string bootstrapperUri in SelectedUriList.ToArray())
            {
                BootstrapperUriList.Remove(bootstrapperUri);
            }
        }

        public async Task<bool> SaveConfigurationCommand()
        {
            IFrontendService frontendService = await ServiceManager.GetWithAwaitAsync<IFrontendService>();
            ILauncherService launcherService = await ServiceManager.GetWithAwaitAsync<ILauncherService>();

            IPeerService peerService = await ServiceManager.GetWithAwaitAsync<IPeerService>();
            SubverseConfig config = await peerService.GetConfigAsync();

            try
            {
                string[] newBootstrapperUriList = BootstrapperUriList.ToArray();
                if (newBootstrapperUriList.Length == 0)
                {
                    throw new SubverseConfig.ValidationException("You must specify at least one Bootstrapper URI.");
                }
                else if (newBootstrapperUriList.Any(x => !Uri.IsWellFormedUriString(x, UriKind.Absolute)))
                {
                    throw new SubverseConfig.ValidationException("One or more Bootstrapper URI(s) are invalid.");
                }
                else
                {
                    config.BootstrapperUriList = newBootstrapperUriList;
                }

                return await peerService.PersistConfigAsync() && frontendService.NavigatePreviousView();
            }
            catch (SubverseConfig.ValidationException ex)
            {
                await launcherService.ShowAlertDialogAsync("Validation error", ex.Message);
            }

            return false;
        }
    }
}
