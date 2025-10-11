using System;

namespace SubverseIM.Android
{
    [Serializable]
    internal class AuthenticationResultException : Exception
    {
        public int ErrorCode { get; }

        public AuthenticationResultException(int errorCode)
        {
            ErrorCode = errorCode;
        }

        public AuthenticationResultException(int errorCode, string? message) : base(message)
        {
            ErrorCode = errorCode;
        }

        public AuthenticationResultException(int errorCode, string? message, Exception? innerException) : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }
}