
namespace SubverseIM.Bootstrapper.Services
{
    [Serializable]
    internal class PeerServiceException : Exception
    {
        public PeerServiceException()
        {
        }

        public PeerServiceException(string? message) : base(message)
        {
        }

        public PeerServiceException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}