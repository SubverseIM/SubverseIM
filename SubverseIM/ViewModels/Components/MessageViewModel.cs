using SubverseIM.Models;

namespace SubverseIM.ViewModels.Components
{
    public class MessageViewModel : ViewModelBase
    {
        private readonly SubverseContact? fromContact;

        private readonly SubverseMessage innerMessage;

        public string Content => innerMessage.Content ?? string.Empty;

        public string DateString => innerMessage
            .DateSignedOn.ToLocalTime()
            .ToString("dd/MM/yy\nHH:mm:ss");

        public string FromName => fromContact?.DisplayName ?? "You";

        public MessageViewModel(SubverseContact? fromContact, SubverseMessage innerMessage)
        {
            this.fromContact = fromContact;
            this.innerMessage = innerMessage;
        }
    }
}
