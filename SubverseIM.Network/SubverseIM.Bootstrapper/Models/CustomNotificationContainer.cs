using Fitomad.Apns.Entities.Notification;
using System.Text.Json.Serialization;

namespace SubverseIM.Bootstrapper.Models
{
    public class CustomNotificationContainer : NotificationContainer 
    {
        [JsonPropertyName("BODY_CONTENT")]
        public string? BodyContent { get; init; }

        [JsonPropertyName("PARTICIPANTS_LIST")]
        public string? ParticipantsList { get; init; }

        [JsonPropertyName("MESSAGE_TOPIC")]
        public string? MessageTopic { get; init; }

        [JsonPropertyName("SENDER_ID")]
        public string? SenderId { get; init; }
    }
}
