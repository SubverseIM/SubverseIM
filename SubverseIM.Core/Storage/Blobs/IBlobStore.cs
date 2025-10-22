namespace SubverseIM.Core.Storage.Blobs
{
    public interface IBlobStore<T>
    {
        Task<BlobStoreDetails?> StoreAsync(IBlobSource<T> source, IProgress<float>? progress = null, CancellationToken cancellationToken = default);
    }
}
