using LiteDB;

namespace SubverseIM.Models
{
    public class SubverseConfig
    {
        public ObjectId? Id { get; set; }

        public string[]? BootstrapperUriList { get; set; }
    }
}
