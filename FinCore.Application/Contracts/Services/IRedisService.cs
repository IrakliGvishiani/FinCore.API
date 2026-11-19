using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Contracts.Services
{
    public interface IRedisService
    {
        Task SetAsync(string key, string value, TimeSpan expiry);

        Task<string?> GetAsync(string key);

        Task RemoveAsync(string key);

        /// <summary>Atomically reads and deletes a key (Redis GETDEL). Returns null if the key did not exist.</summary>
        Task<string?> GetAndRemoveAsync(string key);

        Task<bool> ExistsAsync(string key);

        //Task<bool> TryAcquireConfirmationResendAsync(string userId);

        Task<bool> TryAcquireRateLimitAsync(
            string keyPrefix,
            string userId,
            int maxRequests,
            TimeSpan cooldown,
            TimeSpan window);
    }
}
