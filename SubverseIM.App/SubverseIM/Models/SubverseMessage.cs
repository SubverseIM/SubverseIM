using LiteDB;
using SubverseIM.Core;
using System;

namespace SubverseIM.Models
{
    public class SubverseMessage
    {
        public SubverseMessage()
        {
            Recipients = [];
            RecipientNames = [];
        }

        public ObjectId? Id { get; set; }

        public MessageId? MessageId { get; set; }

        public string? TopicName { get; set; }

        public SubversePeerId Sender { get; set; }

        public string? SenderName { get; set; }

        public SubversePeerId[] Recipients { get; set; }

        public string[] RecipientNames { get; set; }

        public DateTime DateSignedOn { get; set; }

        public string? Content { get; set; }

        public bool? WasDecrypted { get; set; }

        public bool WasDelivered { get; set; }
    }
}