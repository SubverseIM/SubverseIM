using SubverseIM.Core;

namespace SubverseIM.Bootstrapper.Models
{
    public class SubverseMessage
    {
        public long Id { get; set; }

        public string CallId { get; set; } = null!;

        public SubversePeerId OtherPeer { get; set; }
    }
}
