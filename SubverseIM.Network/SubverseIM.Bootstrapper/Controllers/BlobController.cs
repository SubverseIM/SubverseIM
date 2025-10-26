using Microsoft.AspNetCore.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Net.Http.Headers;
using PgpCore;
using SubverseIM.Bootstrapper.Extensions;
using SubverseIM.Core.Storage.Blobs;
using System.IO.Pipelines;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace SubverseIM.Bootstrapper.Controllers
{
    [ApiController]
    [Route("/blob")]
    public class BlobController : ControllerBase
    {
        private const int BLOCK_SIZE_BYTES = 16;

        private const long OFFSET_BITMASK = ~(BLOCK_SIZE_BYTES - 1L);

        private const long MAX_BLOB_SIZE_BYTES = 26_214_400L; // 25 MiB

        private readonly IWebHostEnvironment _environment;

        private readonly IConfiguration _configuration;

        private readonly IDistributedCache _cache;

        private readonly string _blobDirPath;

        private readonly double? _maxBlobAgeHours;

        private readonly bool _enableFeatureFlag;

        public BlobController(IWebHostEnvironment environment, IConfiguration configuration, IDistributedCache cache)
        {
            _environment = environment;
            _configuration = configuration;
            _cache = cache;

            _blobDirPath = Path.Combine(_environment.ContentRootPath, "App_Data", "blob");
            Directory.CreateDirectory(_blobDirPath);

            _maxBlobAgeHours = configuration.GetValue<double?>("Storage:BlobExpireHours");
            _enableFeatureFlag = configuration.GetValue<bool>("Storage:EnableFeature");
        }

        [HttpGet("expire-all")]
        public async Task<IActionResult> DeleteExpiredBlobsAsync(CancellationToken cancellationToken)
        {
            if (_enableFeatureFlag == false)
            {
                return StatusCode((int)HttpStatusCode.Gone, "The server administrator has disabled blob storage.");
            }

            DateTime now = DateTime.UtcNow;
            foreach (FileInfo fileInfo in Directory
                .GetFiles(_blobDirPath)
                .Select(x => new FileInfo(x))
                .Where(x => (now - x.CreationTimeUtc).TotalHours >= _maxBlobAgeHours)) 
            {
                fileInfo.Delete();
            }

            return Ok($"Successfully deleted all blobs older than {_maxBlobAgeHours} hours.");
        }

        [HttpGet("details")]
        public async Task<IActionResult> GetDetailsAsync(CancellationToken cancellationToken) 
        {
            return Ok(new BlobStoreDetails(null, _enableFeatureFlag ? MAX_BLOB_SIZE_BYTES : null));
        }

        [HttpPost("store")]
        [Consumes("application/octet-stream")]
        [Produces("application/octet-stream")]
        [RequestSizeLimit(MAX_BLOB_SIZE_BYTES)]
        public async Task StoreBlobAsync([FromQuery(Name = "p")] string peerIdStr, CancellationToken cancellationToken)
        {
            if (_enableFeatureFlag == false) 
            {
                Response.StatusCode = (int)HttpStatusCode.Gone;
                await Response.WriteAsync("The server administrator has disabled blob storage.");
                return;
            }

            string tempFilePath = Path.GetTempFileName();

            byte[] secretKeyBytes;
            try
            {
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
            }
            catch (BadHttpRequestException) 
            {
                System.IO.File.Delete(tempFilePath);
                throw;
            }

            byte[] blobHashBytes;
            string blobHashStr;
            using (Stream tempFileStream = System.IO.File.OpenRead(tempFilePath))
            {
                blobHashBytes = await SHA256.HashDataAsync(tempFileStream);
                blobHashStr = Convert.ToHexStringLower(blobHashBytes);
            }

            string blobFilePath = Path.Combine(_blobDirPath, blobHashStr);
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
                using (Stream outputStream = new MemoryStream())
                {
                    JsonSerializer.Serialize(inputStream, new BlobStoreResponse(blobHashBytes, secretKeyBytes));
                    inputStream.Seek(0, SeekOrigin.Begin);

                    await pgp.EncryptAsync(inputStream, outputStream);
                    Response.StatusCode = (int)HttpStatusCode.OK;

                    outputStream.Seek(0, SeekOrigin.Begin);
                    await outputStream.CopyToAsync(Response.Body);
                }
            }
            else
            {
                Response.StatusCode = (int)HttpStatusCode.BadRequest;
                await Response.WriteAsync("Peer public key not found.");
            }
        }

        [HttpGet("{id}")]
        [Produces("application/octet-stream")]
        public async Task FetchBlobAsync([FromRoute(Name = "id")] string blobHashStr, [FromQuery(Name = "psk")] string secretKeyStr, CancellationToken cancellationToken)
        {
            if (_enableFeatureFlag == false)
            {
                Response.StatusCode = (int)HttpStatusCode.Gone;
                await Response.WriteAsync("The server administrator has disabled blob storage.");
                return;
            }

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

            string blobFilePath = Path.Combine(_blobDirPath, blobHashStr);
            if (System.IO.File.Exists(blobFilePath))
            {
                using Stream blobFileStream = System.IO.File.OpenRead(blobFilePath);

                long rangeStart = (rangeItemHeaderValue?.From ?? 0) & OFFSET_BITMASK;
                long rangeEnd = (rangeItemHeaderValue?.To + 1) ?? (blobFileStream.Length - BLOCK_SIZE_BYTES);
                if ((rangeEnd & ~OFFSET_BITMASK) > 0)
                {
                    rangeEnd = (rangeEnd & OFFSET_BITMASK) + BLOCK_SIZE_BYTES;
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
                    responseHeaders.ContentRange = new ContentRangeHeaderValue(rangeStart, rangeEnd);
                    responseHeaders.ContentLength = rangeLength;
                }
                else
                {
                    Response.StatusCode = (int)HttpStatusCode.OK;
                }

                try
                {
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
                catch (CryptographicException) { }
            }
            else
            {
                Response.StatusCode = (int)HttpStatusCode.NotFound;
                await Response.WriteAsync($"Blob with ID: {blobHashStr} was not found.", cancellationToken);
            }
        }
    }
}
