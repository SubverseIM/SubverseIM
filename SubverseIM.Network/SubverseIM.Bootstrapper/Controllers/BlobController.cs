using Microsoft.AspNetCore.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Net.Http.Headers;
using PgpCore;
using SubverseIM.Bootstrapper.Extensions;
using System.IO.Pipelines;
using System.Net;
using System.Net.Mime;
using System.Security.Cryptography;
using System.Text.Json;

namespace SubverseIM.Bootstrapper.Controllers
{
    [ApiController]
    [Route("/blob")]
    public class BlobController : ControllerBase
    {
        private const int BLOCK_SIZE_BYTES = 16;

        private const long OFFSET_BITMASK_LEFT = ~(BLOCK_SIZE_BYTES - 1L);

        private const long OFFSET_BITMASK_RIGHT = BLOCK_SIZE_BYTES - 1L;

        private readonly IWebHostEnvironment _environment;

        private readonly IDistributedCache _cache;

        public BlobController(IWebHostEnvironment environment, IDistributedCache cache)
        {
            _environment = environment;
            _cache = cache;
        }

        [HttpPost("store")]
        public async Task<ActionResult> StoreBlobAsync([FromQuery(Name = "p")] string peerIdStr, CancellationToken cancellationToken)
        {
            string tempFilePath = Path.GetTempFileName();

            byte[] secretKeyBytes;
            using (Aes aes = Aes.Create())
            {
                aes.GenerateKey();
                aes.GenerateIV();

                secretKeyBytes = aes.Key;

                using Stream tempFileStream = System.IO.File.OpenWrite(tempFilePath);
                await tempFileStream.WriteAsync(aes.IV, 0, BLOCK_SIZE_BYTES);

                using Stream cryptoStream = new CryptoStream(tempFileStream, aes.CreateEncryptor(), CryptoStreamMode.Write);
                await Request.Body.CopyToAsync(cryptoStream);
            }

            byte[] blobHashBytes;
            using (Stream tempFileStream = System.IO.File.OpenRead(tempFilePath))
            {
                blobHashBytes = await SHA256.HashDataAsync(tempFileStream);
            }

            string blobHashStr = Convert.ToHexStringLower(blobHashBytes);
            string blobFilePath = Path.Combine(_environment.ContentRootPath, "App_Data", "blob", blobHashStr);
            System.IO.File.Move(tempFilePath, blobFilePath);

            byte[]? pkBytes = await _cache.GetAsync($"PKX-{peerIdStr}", cancellationToken);
            if (pkBytes is not null)
            {
                EncryptionKeys keyContainer;
                using (MemoryStream ms = new(pkBytes))
                {
                    keyContainer = new(ms);
                }

                using (PGP pgp = new PGP(keyContainer))
                using (Stream inputStream = new MemoryStream())
                {
                    JsonSerializer.Serialize(inputStream, new
                    {
                        BlobHash = blobHashBytes,
                        SecretKey = secretKeyBytes,
                    });
                    inputStream.Seek(0, SeekOrigin.Begin);

                    Stream outputStream = new MemoryStream();
                    await pgp.EncryptAsync(inputStream, outputStream);

                    outputStream.Seek(0, SeekOrigin.Begin);
                    return new FileStreamResult(outputStream, MediaTypeNames.Application.Octet);
                }
            }
            else
            {
                return BadRequest("Peer public key not found.");
            }
        }

        [HttpGet("{id}")]
        public async Task FetchBlobAsync([FromRoute(Name = "id")] string blobHashStr, [FromQuery(Name = "psk")] string secretKeyStr, CancellationToken cancellationToken)
        {
            byte[] secretKeyBytes;
            try
            {
                secretKeyBytes = Convert.FromHexString(secretKeyStr);
            }
            catch (FormatException) 
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                await Response.WriteAsync("Secret key is not properly formatted.");
                return;
            }

            RangeItemHeaderValue? rangeItemHeaderValue;
            try
            {
                rangeItemHeaderValue = Request.GetTypedHeaders().Range?.Ranges.SingleOrDefault();
            }
            catch (InvalidOperationException)
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                await Response.WriteAsync("Multiple ranges are not supported.", cancellationToken);
                return;
            }

            string blobFilePath = Path.Combine(_environment.ContentRootPath, "App_Data", "blob", blobHashStr);
            if (System.IO.File.Exists(blobFilePath))
            {
                using Stream blobFileStream = System.IO.File.OpenRead(blobFilePath);

                long rangeStart = (rangeItemHeaderValue?.From ?? 0) & OFFSET_BITMASK_LEFT;
                long rangeEnd = rangeItemHeaderValue?.To ?? (blobFileStream.Length - BLOCK_SIZE_BYTES);
                if ((rangeEnd & OFFSET_BITMASK_RIGHT) > 0)
                {
                    rangeEnd = (rangeEnd & OFFSET_BITMASK_LEFT) + BLOCK_SIZE_BYTES;
                }

                long rangeLength;
                if (rangeStart < 0 || rangeEnd <= 0 ||
                    rangeStart >= (blobFileStream.Length - BLOCK_SIZE_BYTES) || 
                    rangeEnd > (blobFileStream.Length - BLOCK_SIZE_BYTES) ||
                    rangeStart > rangeEnd)
                {
                    Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    await Response.WriteAsync("Invalid range was specified.");
                    return;
                }
                else if (rangeStart == rangeEnd)
                {
                    Response.StatusCode = (int)HttpStatusCode.OK;
                    return;
                }
                else
                {
                    rangeLength = rangeEnd - rangeStart;
                }

                ResponseHeaders responseHeaders = Response.GetTypedHeaders();
                if (rangeItemHeaderValue is not null)
                {
                    Response.StatusCode = (int)HttpStatusCode.PartialContent;
                    responseHeaders.ContentRange = new ContentRangeHeaderValue(rangeStart, rangeEnd, rangeLength);
                    responseHeaders.ContentLength = rangeLength;
                }
                else
                {
                    Response.StatusCode = (int)HttpStatusCode.OK;
                }

                byte[] prevBlockBytes = new byte[BLOCK_SIZE_BYTES];
                blobFileStream.Seek(rangeStart, SeekOrigin.Begin);
                blobFileStream.ReadExactly(prevBlockBytes, 0, prevBlockBytes.Length);

                using (Aes aes = Aes.Create())
                using (CryptoStream cryptoStream = new CryptoStream(blobFileStream,
                    aes.CreateDecryptor(secretKeyBytes, prevBlockBytes), CryptoStreamMode.Read))
                {
                    await cryptoStream.CopyToAsync(Response.Body, rangeLength, cancellationToken);
                }
            }
            else
            {
                Response.StatusCode = (int)HttpStatusCode.NotFound;
                await Response.WriteAsync($"Blob with ID: {blobHashStr} was not found.", cancellationToken);
            }
        }
    }
}
