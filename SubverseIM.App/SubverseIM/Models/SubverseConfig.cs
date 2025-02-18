using LiteDB;
using System;

namespace SubverseIM.Models
{
    public class SubverseConfig
    {
        public class ValidationException : Exception
        {
            public ValidationException()
            {
            }

            public ValidationException(string? message) : base(message)
            {
            }

            public ValidationException(string? message, Exception? innerException) : base(message, innerException)
            {
            }
        }

        public ObjectId? Id { get; set; }

        public string[]? BootstrapperUriList { get; set; }

        public bool MessageOrderFlag { get; set; }

        public DateTime DateLastPrompted { get; set; }

        public int? PromptFreqIndex { get; set; }
    }
}
