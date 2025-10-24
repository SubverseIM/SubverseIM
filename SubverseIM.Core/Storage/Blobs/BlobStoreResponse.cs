namespace SubverseIM.Core.Storage.Blobs;

public record class BlobStoreResponse(byte[] BlobHash, byte[] SecretKey);