namespace SubverseIM
{
    public class SubverseMessage
    {
        public SubversePeerId Recipient { get; }

        public byte[] Content { get; }

        public SubverseMessage(SubversePeerId recipient, byte[] content) 
        {
            Recipient = recipient;
            Content = content;
        }
    }
}