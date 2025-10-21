// See https://aka.ms/new-console-template for more information

using PgpCore;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;

using HttpClient httpClient = new() { BaseAddress = new Uri(args[0]) };

EncryptionKeys keyContainer;
string peerIdStr;

using (MemoryStream privateKeyStream = new())
using (MemoryStream publicKeyStream = new())
using (PGP pgp = new PGP())
{
    await pgp.GenerateKeyAsync(publicKeyStream, privateKeyStream, password: "#FreeTheInternet");

    publicKeyStream.Position = 0;
    using HttpResponseMessage _ = await httpClient.PostAsync("pk", new StreamContent(publicKeyStream)
    { Headers = { ContentType = new("application/pgp-keys") } });

    publicKeyStream.Position = 0;
    privateKeyStream.Position = 0;

    keyContainer = new(publicKeyStream, privateKeyStream, "#FreeTheInternet");
    peerIdStr = Convert.ToHexStringLower(keyContainer.PublicKey.GetFingerprint());
}

BlobStoreDetails? blobStoreDetails;
using (FileStream blobFileStream = File.OpenRead(args[1]))
{
    string encryptedResponseStr;
    using (HttpResponseMessage response = await httpClient.PostAsync($"blob/store?p={peerIdStr}", new StreamContent(blobFileStream)
    { Headers = { ContentType = new("application/octet-stream") } }))
    {
        response.EnsureSuccessStatusCode();
        encryptedResponseStr = await response.Content.ReadAsStringAsync();
    }

    using (PGP pgp = new PGP(keyContainer))
    {
        string decryptedResponseStr = await pgp.DecryptAsync(encryptedResponseStr);
        blobStoreDetails = JsonSerializer.Deserialize<BlobStoreDetails>(decryptedResponseStr);
    }
}

string? blobHashStr = blobStoreDetails?.BlobHash is null ?
    null : Convert.ToHexStringLower(blobStoreDetails.BlobHash);
string? secretKeyStr = blobStoreDetails?.SecretKey is null ?
    null : Convert.ToHexStringLower(blobStoreDetails.SecretKey);

Debug.Assert(blobHashStr is not null && secretKeyStr is not null);

Console.WriteLine(new Uri(httpClient.BaseAddress, $"blob/{blobHashStr}?psk={secretKeyStr}"));

class BlobStoreDetails
{
    public byte[]? BlobHash { get; set; }

    public byte[]? SecretKey { get; set; }
}