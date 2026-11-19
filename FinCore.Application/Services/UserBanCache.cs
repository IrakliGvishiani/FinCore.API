using FinCore.Application.Contracts.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FinCore.Application.Services
{
    public class UserBanCache : IUserBanCache
    {
        // Covers clock differences between app instances and Redis; the JWT ClockSkew is zero.
        public static readonly TimeSpan Margin = TimeSpan.FromMinutes(2);

        private readonly IRedisService _redis;
        private readonly IConfiguration _configuration;
        private readonly ILogger<UserBanCache> _logger;

        public UserBanCache(IRedisService redis, IConfiguration configuration, ILogger<UserBanCache> logger)
        {
            _redis = redis;
            _configuration = configuration;
            _logger = logger;
        }

        public static string Key(string userId) => $"banned-user:{userId}";

        public TimeSpan Ttl
        {
            get
            {
                var raw = _configuration["Jwt:AccessTokenExpiryMinutes"];

                if (!int.TryParse(raw, out var minutes) || minutes <= 0)
                    throw new InvalidOperationException(
                        "Jwt:AccessTokenExpiryMinutes is not configured or is not a positive integer.");

                return TimeSpan.FromMinutes(minutes) + Margin;
            }
        }

        public async Task MarkBannedAsync(string userId)
        {
            // Read outside the try so a misconfiguration surfaces instead of being swallowed.
            var ttl = Ttl;

            try
            {
                await _redis.SetAsync(Key(userId), "1", ttl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to set banned-user marker for {UserId}; existing access tokens stay valid until they expire.",
                    userId);
            }
        }

        public async Task ClearAsync(string userId)
        {
            try
            {
                await _redis.RemoveAsync(Key(userId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to remove banned-user marker for {UserId}; new access tokens are rejected until it expires.",
                    userId);
            }
        }

        public async Task<bool> IsBannedAsync(string userId)
        {
            try
            {
                return await _redis.ExistsAsync(Key(userId));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Banned-user check failed for {UserId}; allowing the request (fail open).",
                    userId);
                return false;
            }
        }
    }
}
