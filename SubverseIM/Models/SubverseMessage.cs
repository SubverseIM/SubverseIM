using System;

namespace SubverseIM.Models
{
    public class SubverseMessage
    {
        public SubversePeerId Sender { get; set; }

        public SubversePeerId Recipient { get; set; }

        public DateTime DateSignedOn { get; set; }

        public string? Content { get; set; }
    }
}