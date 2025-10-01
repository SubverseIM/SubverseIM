using Fitomad.Apns.Entities.Notification;
using System.Text.Json.Serialization;

namespace SubverseIM.Bootstrapper.Models
{
    public class CustomNotificationContainer : NotificationContainer 
    {
        [JsonPropertyName("com.ChosenFewSoftware.SubverseIM.BodyContent")]
        public string? BodyContent { get; init; }

        [JsonPropertyName("com.ChosenFewSoftware.SubverseIM.ConversationParticipants")]
        public string? ConversationParticipants { get; init; }

        [JsonPropertyName("com.ChosenFewSoftware.SubverseIM.MessageTopic")]
        public string? MessageTopic { get; init; }

        [JsonPropertyName("com.ChosenFewSoftware.SubverseIM.SenderId")]
        public string? SenderId { get; init; }
    }
}
