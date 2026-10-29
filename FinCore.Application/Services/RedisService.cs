using FinCore.Application.Contracts.Services;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Services
{
    public class RedisService : IRedisService
    {
        private readonly IDatabase _database;

        public RedisService(IConnectionMultiplexer connectionMultiplexer)
        {
            _database = connectionMultiplexer.GetDatabase();
        }
        public async Task<string?> GetAsync(string key)
        {
            return await _database.StringGetAsync(key);
        }

        public async Task RemoveAsync(string key)
        {
            await _database.KeyDeleteAsync(key);
        }


        public async Task SetAsync(string key, string value, TimeSpan expiry)
        {
            await _database.StringSetAsync(key, value, expiry);
        }

        public async Task<bool> TryAcquireRateLimitAsync(
            string keyPrefix,
            string userId,
            int maxRequests,
            TimeSpan cooldown,
            TimeSpan window)
        {
            var cooldownKey = $"{keyPrefix}:cooldown:{userId}";
            var countKey = $"{keyPrefix}:count:{userId}";

            const string script = """
        if redis.call('EXISTS', KEYS[1]) == 1 then
            return 0
        end

        local count = tonumber(redis.call('GET', KEYS[2]) or '0')

        if count >= tonumber(ARGV[1]) then
            return 0
        end

        redis.call('SET', KEYS[1], '1', 'PX', ARGV[2])
        local newCount = redis.call('INCR', KEYS[2])

        if newCount == 1 then
            redis.call('PEXPIRE', KEYS[2], ARGV[3])
        end

        return 1
        """;

            var result = await _database.ScriptEvaluateAsync(
                script,
                new RedisKey[] { cooldownKey, countKey },
                new RedisValue[]
                {
            maxRequests,
            (long)cooldown.TotalMilliseconds,
            (long)window.TotalMilliseconds
                });

            return (int)result == 1;
        }
    }
    }
