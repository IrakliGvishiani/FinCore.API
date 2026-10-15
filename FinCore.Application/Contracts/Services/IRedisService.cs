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

        //Task<bool> TryAcquireConfirmationResendAsync(string userId);

        Task<bool> TryAcquireRateLimitAsync(
            string keyPrefix,
            string userId,
            int maxRequests,
            TimeSpan cooldown,
            TimeSpan window);
    }
}
