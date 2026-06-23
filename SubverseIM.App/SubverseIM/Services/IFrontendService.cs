using SubverseIM.Models;
using System;
using System.Threading.Tasks;

namespace SubverseIM.Services
{
    public class MessageReceivedEventArgs : EventArgs
    {
        private bool shouldSendPushNotif;
        public bool ShouldSendPushNotif
        {
            get => shouldSendPushNotif;
            set => shouldSendPushNotif &= value;
        }

        public SubverseMessage Message { get; }

        public MessageReceivedEventArgs(SubverseMessage message)
        {
            shouldSendPushNotif = true;
            Message = message;
        }
    }

    public interface IFrontendService : IRunnable, IBackgroundRunnable
    {
        Task ResetSizeAsync();

        Task RestorePurchasesAsync();

        event EventHandler<MessageReceivedEventArgs>? MessageReceived;
    }
}
