using Microsoft.AspNetCore.Mvc.Testing;
using PgpCore;
using SubverseIM.Core;
using System.Security.Cryptography;
using Xunit;

namespace SubverseIM.Bootstrapper.Tests;

public class IntegrationTests
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public IntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/index.html")]
    [InlineData("/privacy.html")]
    public async Task Get_StaticPagesReturnSuccessAndCorrectContentType(string url)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(url);

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/html", response.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task Get_CreateInviteUrlReturnsSuccessAndJson()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/invite?p={RandomNumberGenerator.GetHexString(40, true)}?t=1.5");
        response.EnsureSuccessStatusCode();

        string? result = await response.Content.ReadFromJsonAsync<string>();
        Assert.False(string.IsNullOrEmpty(result));
    }

    [Fact]
    public async Task Post_SubmitPublicKeyReturnsSuccessAndJson()
    {
        var client = _factory.CreateClient();

        using (var pgp = new PGP())
        using (var publicKeyStream = new MemoryStream())
        using (var privateKeyStream = new MemoryStream())
        {
            await pgp.GenerateKeyAsync(publicKeyStream, privateKeyStream);
            publicKeyStream.Position = 0;
            privateKeyStream.Position = 0;

            var response = await client.PostAsync("/pk", new StreamContent(publicKeyStream)
            { Headers = { ContentType = new("application/pgp-keys") } });
            response.EnsureSuccessStatusCode();

            bool? result = await response.Content.ReadFromJsonAsync<bool>();
            Assert.Equal(true, result);
        }
    }

    [Fact]
    public async Task Post_SynchonizeNodesReturnsSuccessAndJson()
    {
        var client = _factory.CreateClient();

        EncryptionKeys myKeys;
        SubversePeerId myPeerId;
        using (var pgp = new PGP())
        using (var publicKeyStream = new MemoryStream())
        using (var privateKeyStream = new MemoryStream())
        {
            await pgp.GenerateKeyAsync(publicKeyStream, privateKeyStream, password: "#FreeTheInternet");
            publicKeyStream.Position = 0;
            privateKeyStream.Position = 0;

            myKeys = new EncryptionKeys(publicKeyStream, privateKeyStream, "#FreeTheInternet");
            myPeerId = new(myKeys.PublicKey.GetFingerprint());
        }

        using (var pgp = new PGP(myKeys))
        using (var inputStream = new MemoryStream(RandomNumberGenerator.GetBytes(1024)))
        using (var outputStream = new MemoryStream())
        {
            await pgp.SignStreamAsync(inputStream, outputStream);
            outputStream.Position = 0;

            var response = await client.PostAsync($"/nodes?p={myPeerId}", new StreamContent(outputStream)
            { Headers = { ContentType = new("application/octet-stream") } });
            response.EnsureSuccessStatusCode();

            bool? result = await response.Content.ReadFromJsonAsync<bool>();
            Assert.Equal(false, result);
        }
    }

    [Fact]
    public async Task Get_SynchronizeNodesReturnsSuccessAndBytes() 
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/nodes?p={RandomNumberGenerator.GetHexString(40, true)}");

        response.EnsureSuccessStatusCode();
        Assert.Equal("application/octet-stream", response.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task Get_RelayWithHttpThrowsError()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/relay");
        Assert.Throws<HttpRequestException>(response.EnsureSuccessStatusCode);
    }

    [Fact]
    public async Task Get_CacheTopicIdReturnsSuccessAndJson() 
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/topic");
        response.EnsureSuccessStatusCode();

        string? result = await response.Content.ReadFromJsonAsync<string>();
        Assert.False(string.IsNullOrEmpty(result));
    }
}