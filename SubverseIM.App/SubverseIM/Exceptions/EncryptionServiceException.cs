using System;

namespace SubverseIM.Exceptions
{
    [Serializable]
    public class EncryptionServiceException : Exception
    {
        public EncryptionServiceException()
        {
        }

        public EncryptionServiceException(string? message) : base(message)
        {
        }

        public EncryptionServiceException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}