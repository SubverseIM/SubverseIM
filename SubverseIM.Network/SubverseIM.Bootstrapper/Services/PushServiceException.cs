
namespace SubverseIM.Bootstrapper.Services
{
    [Serializable]
    public class PushServiceException : Exception
    {
        public PushServiceException()
        {
        }

        public PushServiceException(string? message) : base(message)
        {
        }

        public PushServiceException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}