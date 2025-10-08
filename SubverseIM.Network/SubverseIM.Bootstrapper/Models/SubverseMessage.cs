using LiteDB;
using SubverseIM.Core;

namespace SubverseIM.Bootstrapper.Models
{
    public class SubverseMessage
    {
        public ObjectId? Id { get; set; }

        public MessageId? MessageId { get; set; }
    }
}
