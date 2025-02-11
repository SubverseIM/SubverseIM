using SubverseIM.Models;
using System;
using System.IO;

namespace SubverseIM.Serializers
{
    public class CsvMessageSerializer : ISerializer<SubverseMessage>, IDisposable
    {
        private readonly StreamWriter writer;

        public CsvMessageSerializer(Stream outputStream) 
        {
            writer = new StreamWriter(outputStream) { AutoFlush = true };
            writer.WriteLine("dateReceived,fromUser,toUsers");
        }

        public void Serialize(SubverseMessage value)
        {
            writer.WriteLine($"{value.DateSignedOn:g},{value.SenderName},{string.Join(';', value.RecipientNames)}");
        }

        public void Dispose()
        {
            ((IDisposable)writer).Dispose();
        }
    }
}
