using LiteDB;
using SubverseIM.Core.Storage.Messages;

namespace SubverseIM.Bootstrapper.Models
{
    public class SubverseMessage
    {
        public ObjectId? Id { get; set; }

        public MessageId? MessageId { get; set; }
    }
}
