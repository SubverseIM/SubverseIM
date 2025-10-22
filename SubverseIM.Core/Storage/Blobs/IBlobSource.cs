namespace SubverseIM.Core.Storage.Blobs
{
    public interface IBlobSource<T>
    {
        Task<T> RetrieveAsync(CancellationToken cancellationToken = default);
    }
}