using LiteDB;
using System;

namespace SubverseIM.Models
{
    public class SubverseMessage
    {
        public SubverseMessage() 
        {
            Recipients = [];
        }

        public ObjectId? Id { get; set; }

        public string? CallId { get; set; }

        public string? TopicName { get; set; }

        public SubversePeerId Sender { get; set; }

        public SubversePeerId[] Recipients { get; set; }

        public DateTime DateSignedOn { get; set; }

        public string? Content { get; set; }

        public bool WasDelivered { get; set; }
    }
}