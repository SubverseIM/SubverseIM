using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using PgpCore;
using SubverseIM.Bootstrapper.Services;
using SubverseIM.Core;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;

namespace SubverseIM.Bootstrapper.Controllers
{
    [ApiController]
    [Route("/")]
    public class SubverseController : ControllerBase
    {
        private readonly IDistributedCache _cache;

        private readonly ILogger<SubverseController> _logger;

        public SubverseController(IConfiguration configuration, IDistributedCache cache, ILogger<SubverseController> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        [HttpGet("relay")]
        public async Task StartRelayAsync([FromQuery(Name = "p")] string? peerIdStr, CancellationToken cancellationToken)
        {
            if (HttpContext.WebSockets.IsWebSocketRequest && !string.IsNullOrEmpty(peerIdStr))
            {
                WebSocket webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();

                SubversePeerId peerId = SubversePeerId.FromString(peerIdStr);
                PeerService peer = new PeerService(webSocket, peerId, _logger);

                await Task.WhenAll(
                    peer.ListenSocketAsync(cancellationToken),
                    peer.SendSocketAsync(cancellationToken)
                    );
            }
            else
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            }
        }

        [HttpGet("invite/{id}")]
        public async Task<ActionResult> UseInviteAsync([FromRoute(Name = "id")] string inviteId)
        {
            string? peerUri = await _cache.GetStringAsync($"INV-{inviteId}");
            if (peerUri is not null)
            {
                return Redirect(peerUri);
            }
            else
            {
                return NotFound();
            }
        }

        [HttpGet("invite")]
        [Produces("application/json")]
        public async Task<string> CreateInviteAsync([FromQuery(Name = "p")] string peerIdStr, [FromQuery(Name = "t")] double expireTimeHrs, CancellationToken cancellationToken)
        {
            string inviteId = Guid.NewGuid().ToString();
            await _cache.SetStringAsync(
                    $"INV-{inviteId}", $"sv://{peerIdStr}",
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpiration = DateTimeOffset.Now + TimeSpan.FromHours(expireTimeHrs)
                    },
                    cancellationToken);
            return inviteId;
        }

        [HttpGet("pk")]
        [Produces("application/pgp-keys")]
        public async Task GetPublicKey([FromQuery(Name = "p")] string peerIdStr, CancellationToken cancellationToken)
        {
            byte[] responseBytes = await _cache.GetAsync($"PKX-{peerIdStr}", cancellationToken) ?? [];
            await Response.Body.WriteAsync(responseBytes, cancellationToken);
        }

        [HttpPost("pk")]
        [Consumes("application/pgp-keys")]
        [Produces("application/json")]
        public async Task<bool> SubmitPublicKey(CancellationToken cancellationToken)
        {
            try
            {
                EncryptionKeys keyContainer;
                using (var streamReader = new StreamReader(Request.Body, Encoding.ASCII))
                {
                    keyContainer = new EncryptionKeys(await streamReader.ReadToEndAsync());
                }

                SubversePeerId peerId = new(keyContainer.PublicKey.GetFingerprint());
                _logger.LogInformation($"PKX Submitted: {peerId}");

                await _cache.SetAsync(
                    $"PKX-{peerId}", keyContainer.PublicKey.GetEncoded(),
                    new DistributedCacheEntryOptions { AbsoluteExpiration = null },
                    cancellationToken);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, null);
                return false;
            }
        }

        [HttpGet("nodes")]
        [Produces("application/octet-stream")]
        public async Task GetNodesAsync([FromQuery(Name = "p")] string peerIdStr, CancellationToken cancellationToken)
        {
            byte[] responseBytes = await _cache.GetAsync($"DAT-{peerIdStr}", cancellationToken) ?? [];
            Response.GetTypedHeaders().ContentType = new("application/octet-stream");
            await Response.Body.WriteAsync(responseBytes, cancellationToken);
        }

        [HttpPost("nodes")]
        [Consumes("application/octet-stream")]
        [Produces("application/json")]
        public async Task<bool> PostNodesAsync([FromQuery(Name = "p")] string peerIdStr, CancellationToken cancellationToken)
        {
            try
            {
                byte[]? pkBytes = await _cache.GetAsync($"PKX-{peerIdStr}", cancellationToken);
                if (pkBytes is not null)
                {
                    EncryptionKeys keyContainer;
                    using (MemoryStream ms = new(pkBytes))
                    {
                        keyContainer = new(ms);
                    }

                    byte[] nodesBytes;
                    bool verifySuccess;
                    using (PGP pgp = new(keyContainer))
                    using (MemoryStream inputStream = new())
                    using (MemoryStream outputStream = new())
                    {
                        await Request.Body.CopyToAsync(inputStream);
                        inputStream.Position = 0;

                        verifySuccess = await pgp.VerifyAsync(inputStream, outputStream);
                        nodesBytes = outputStream.ToArray();
                    }

                    if (verifySuccess)
                    {
                        SubversePeerId peerId = new(keyContainer.PublicKey.GetFingerprint());
                        await _cache.SetAsync($"DAT-{peerId}", nodesBytes,
                            new DistributedCacheEntryOptions { AbsoluteExpiration = null }
                            );
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, null);
            }

            return false;
        }

        [HttpGet("topic")]
        [Produces("application/json")]
        public async Task<string> GetTopicIdAsync(CancellationToken cancellationToken)
        {
            string? topicStr = await _cache.GetStringAsync("TOPIC-ID");
            if (string.IsNullOrEmpty(topicStr))
            {
                topicStr = RandomNumberGenerator.GetHexString(40);
                await _cache.SetStringAsync("TOPIC-ID", topicStr,
                    new DistributedCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15)
                    }, cancellationToken);
            }
            return topicStr;
        }
    }
}
