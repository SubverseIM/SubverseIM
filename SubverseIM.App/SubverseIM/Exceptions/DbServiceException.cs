using System;

namespace SubverseIM.Exceptions
{
    [Serializable]
    public class DbServiceException : Exception
    {
        public DbServiceException()
        {
        }

        public DbServiceException(string? message) : base(message)
        {
        }

        public DbServiceException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}