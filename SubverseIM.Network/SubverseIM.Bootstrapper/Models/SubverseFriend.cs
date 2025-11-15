using LiteDB;

namespace SubverseIM.Bootstrapper.Models
{
    public class SubverseFriend
    {
        public ObjectId? Id { get; set; }

        public Uri? Address { get; set; }

        public DateTime? LastSeenOn { get; set; }
    }
}
