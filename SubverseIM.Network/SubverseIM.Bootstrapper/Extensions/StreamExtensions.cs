namespace SubverseIM.Bootstrapper.Extensions
{
    public static class StreamExtensions
    {
        public static void CopyTo(this Stream source, Stream destination, long length, int bufferSize)
        {
            if (length == 0) return;
            byte[] buffer = new byte[bufferSize];

            int read;
            while ((read = source.Read(buffer, 0, (int)Math.Min(buffer.Length, length))) > 0)
            {
                destination.Write(buffer, 0, read);
                length -= read;
            }
        }

        public static void CopyTo(this Stream source, Stream destination, long length) 
        {
            CopyTo(source, destination, length, 81920);
        }

        public static async Task CopyToAsync(this Stream source, Stream destination, long length, int bufferSize, CancellationToken cancellationToken) 
        {
            if (length == 0) return;
            byte[] buffer = new byte[bufferSize];

            int read;
            while ((read = await source.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, length), cancellationToken)) > 0)
            {
                await destination.WriteAsync(buffer, 0, read, cancellationToken);
                length -= read;
            }
        }

        public static Task CopyToAsync(this Stream source, Stream destination, long length, CancellationToken cancellationToken)
        {
            return CopyToAsync(source, destination, length, 81920, cancellationToken);
        }

        public static Task CopyToAsync(this Stream source, Stream destination, long length, int bufferSize) 
        {
            return CopyToAsync(source, destination, length, bufferSize, CancellationToken.None);
        }

        public static Task CopyToAsync(this Stream source, Stream destination, long length)
        {
            return CopyToAsync(source, destination, length, 81920, CancellationToken.None);
        }
    }
}
