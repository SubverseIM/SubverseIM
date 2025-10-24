namespace SubverseIM.Core.Storage.Blobs
{
    public interface IBlobStore<T>
    {
        Task<BlobStoreDetails> GetDetailsAsync(CancellationToken cancellationToken = default);

        Task<BlobStoreResponse> StoreBlobAsync(IBlobSource<T> source, IProgress<float>? progress = null, CancellationToken cancellationToken = default);
    }
}
