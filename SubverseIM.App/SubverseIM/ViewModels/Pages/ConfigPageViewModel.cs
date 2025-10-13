using Avalonia.Media;
using ReactiveUI;
using SubverseIM.Models;
using SubverseIM.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace SubverseIM.ViewModels.Pages
{
    public class ConfigPageViewModel : PageViewModelBase<ConfigPageViewModel>
    {
        public override bool HasSidebar => false;

        public override string Title => "Settings View";

        public bool IsRunningOnIOS => OperatingSystem.IsIOS();

        public ObservableCollection<string> BootstrapperUriList { get; }

        public ObservableCollection<string> SelectedUriList { get; }

        public ObservableCollection<string> ThemeVariantList { get; }

        private bool isFormattingAllowed;
        public bool IsFormattingAllowed
        {
            get => isFormattingAllowed;
            set
            {
                this.RaiseAndSetIfChanged(ref isFormattingAllowed, value);
            }
        }

        private bool messageOrderFlag;
        public bool MessageOrderFlag
        {
            get => messageOrderFlag;
            set
            {
                this.RaiseAndSetIfChanged(ref messageOrderFlag, value);
            }
        }

        private bool messageMirrorFlag;
        public bool MessageMirrorFlag
        {
            get => messageMirrorFlag;
            set
            {
                this.RaiseAndSetIfChanged(ref messageMirrorFlag, value);
            }
        }

        private Color defaultChatColor;
        public Color DefaultChatColor
        {
            get => defaultChatColor;
            set
            {
                IsChatColorDefault = false;
                this.RaiseAndSetIfChanged(ref defaultChatColor, value);
            }
        }

        private bool isChatColorDefault;
        public bool IsChatColorDefault
        {
            get => isChatColorDefault;
            set
            {
                this.RaiseAndSetIfChanged(ref isChatColorDefault, value);
            }
        }

        private int? promptFreqIndex;
        public int? PromptFreqIndex
        {
            get => promptFreqIndex;
            set
            {
                this.RaiseAndSetIfChanged(ref promptFreqIndex, value);
            }
        }

        private bool isPushNotificationsEnabled;
        public bool IsPushNotificationsEnabled
        {
            get => isPushNotificationsEnabled;
            set 
            {
                this.RaiseAndSetIfChanged(ref isPushNotificationsEnabled, value);
            }
        }

        public ConfigPageViewModel(IServiceManager serviceManager) : base(serviceManager)
        {
            BootstrapperUriList = new();
            SelectedUriList = new();
            ThemeVariantList = new() { "Default", "Light", "Dark" };
        }

        public async Task InitializeAsync()
        {
            IConfigurationService peerService = await ServiceManager.GetWithAwaitAsync<IConfigurationService>();
            SubverseConfig config = await peerService.GetConfigAsync();

            BootstrapperUriList.Clear();
            foreach (string bootstrapperUri in config.BootstrapperUriList ?? [])
            {
                BootstrapperUriList.Add(bootstrapperUri);
            }

            IsFormattingAllowed = config.IsFormattingAllowed;

            MessageOrderFlag = config.MessageOrderFlag;

            MessageMirrorFlag = config.MessageMirrorFlag;

            DefaultChatColor = config.DefaultChatColorCode is null ? Colors.MediumPurple :
                Color.FromUInt32(config.DefaultChatColorCode.Value);
            IsChatColorDefault = config.DefaultChatColorCode is null;

            PromptFreqIndex = config.PromptFreqIndex;

            IsPushNotificationsEnabled = config.IsPushNotificationsEnabled;

            UseThemeOverride = config.UseThemeOverride ?? "Default";
        }

        public async Task AddBootstrapperUriCommand(string? defaultText = null)
        {
            ILauncherService launcherService = await ServiceManager.GetWithAwaitAsync<ILauncherService>();
            string? newBootstrapperUri = await launcherService.ShowInputDialogAsync("New Bootstrapper URI", defaultText ?? "https://subverse.network/");
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

            IConfigurationService peerService = await ServiceManager.GetWithAwaitAsync<IConfigurationService>();
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

                config.IsFormattingAllowed = IsFormattingAllowed;

                config.MessageOrderFlag = MessageOrderFlag;

                config.MessageMirrorFlag = messageMirrorFlag;

                config.DefaultChatColorCode = DefaultChatColor == Colors.MediumPurple ?
                    null : DefaultChatColor.ToUInt32();

                config.PromptFreqIndex = PromptFreqIndex == 3 ? null : PromptFreqIndex;

                config.IsPushNotificationsEnabled = IsPushNotificationsEnabled;

                config.UseThemeOverride = UseThemeOverride;

                return await peerService.PersistConfigAsync() && frontendService.NavigatePreviousView();
            }
            catch (SubverseConfig.ValidationException ex)
            {
                await launcherService.ShowAlertDialogAsync("Validation error", ex.Message);
            }

            return false;
        }

        public void ResetDefaultChatColorCommand()
        {
            DefaultChatColor = Colors.MediumPurple;
            IsChatColorDefault = true;
        }
    }
}
