using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SubverseIM.Bootstrapper.Filters;
using SubverseIM.Bootstrapper.Models;
using SubverseIM.Bootstrapper.Services;
using SubverseIM.Core.Storage.Friends;

namespace SubverseIM.Bootstrapper.Controllers
{
    [ApiController]
    [Route("/friends")]
    public class FriendsController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        private readonly IDbService _dbService;

        private readonly HttpClient _httpClient;

        public FriendsController(IConfiguration configuration, IDbService dbService, HttpClient httpClient)
        {
            _configuration = configuration;
            _dbService = dbService;
            _httpClient = httpClient;
        }

        [HttpPost("announce")]
        public IActionResult PostAnnounce([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Disallow)] FriendDetails friend)
        {
            _dbService.InsertFriend(new SubverseFriend
            {
                Address = friend.Address,
                LastSeenOn = friend.LastSeenOn
            });

            return Ok();
        }

        [HttpGet("announce")]
        [HostFilteringActionFilter(["localhost"])]
        public async Task<IActionResult> ExecuteAnnounce(CancellationToken cancellationToken)
        {
            FriendDetails friendDetails;

            string? myAddressStr = _configuration.GetValue<string>("Friends:MyAddress");
            if (myAddressStr is null)
            {
                return StatusCode(StatusCodes.Status410Gone);
            }
            else
            {
                Uri myAddress = new Uri(myAddressStr);
                friendDetails = new FriendDetails(myAddress, DateTime.UtcNow);
            }

            int? maxListCount = _configuration.GetValue<int?>("Friends:MaxListCount");

            double? expireTimeMinutes = _configuration.GetValue<double?>("Friends:ExpireTimeMinutes");
            TimeSpan? expireTime = expireTimeMinutes.HasValue ?
                TimeSpan.FromMinutes(expireTimeMinutes.Value) :
                null;

            IEnumerable<Uri> announce = [.. _configuration
                .GetValue<string[]>("Friends:Announce")?
                .Select(x => new Uri(x)) ?? [], .. _dbService
                .GetRecentFriends(maxListCount, expireTime)
                .Where(x => x.Address is not null)
                .Select(x => x.Address!)];

            foreach (Uri announceEndPoint in announce.Select(x => new Uri(x, "/friends/announce")))
            {
                try
                {
                    await _httpClient.PostAsJsonAsync(announceEndPoint, friendDetails, cancellationToken);
                }
                catch (HttpRequestException) { }
            }

            return Ok();
        }

        [HttpGet("synchronize")]
        [HostFilteringActionFilter(["localhost"])]
        public async Task<IActionResult> ExecuteSynchronize(CancellationToken cancellationToken)
        {
            int? maxListCount = _configuration.GetValue<int?>("Friends:MaxListCount");

            double? expireTimeMinutes = _configuration.GetValue<double?>("Friends:ExpireTimeMinutes");
            TimeSpan? expireTime = expireTimeMinutes.HasValue ?
                TimeSpan.FromMinutes(expireTimeMinutes.Value) :
                null;

            IEnumerable<Uri> announce = [.. _configuration
                .GetValue<string[]>("Friends:Synchronize")?
                .Select(x => new Uri(x)) ?? [], .. _dbService
                .GetRecentFriends(maxListCount, expireTime)
                .Where(x => x.Address is not null)
                .Select(x => x.Address!)];

            List<FriendDetails> fetchedList = new();
            foreach (Uri fetchEndPoint in announce.Select(x => new Uri(x, "/friends")))
            {
                try
                {
                    fetchedList.AddRange(await _httpClient.GetFromJsonAsync
                        <FriendDetails[]>(fetchEndPoint, cancellationToken) ?? 
                        []);
                }
                catch (HttpRequestException) { }
            }

            foreach (FriendDetails friendDetails in fetchedList.DistinctBy(x => x.Address))
            {
                _dbService.InsertFriend(new SubverseFriend
                {
                    Address = friendDetails.Address,
                    LastSeenOn = friendDetails.LastSeenOn
                });
            }

            return Ok();
        }


        [HttpGet]
        public IActionResult GetFriendsList() 
        {
            int? maxListCount = _configuration.GetValue<int?>("Friends:MaxListCount");

            double? expireTimeMinutes = _configuration.GetValue<double?>("Friends:ExpireTimeMinutes");
            TimeSpan? expireTime = expireTimeMinutes.HasValue ?
                TimeSpan.FromMinutes(expireTimeMinutes.Value) :
                null;

            FriendDetails[] resultArr = _dbService
                .GetRecentFriends(maxListCount, expireTime)
                .Where(x => x.Address is not null && x.LastSeenOn is not null)
                .Select(x => new FriendDetails(x.Address!, x.LastSeenOn!.Value))
                .ToArray();

            return Ok(resultArr);
        }
    }
}
