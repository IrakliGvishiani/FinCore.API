using FinCore.Application.Contracts.Services;
using FinCore.Application.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FinCore.Tests.Services;

public class UserBanCacheTests
{
    private readonly Mock<IRedisService> _redis = new();
    private readonly Mock<IConfiguration> _config = new();

    private UserBanCache CreateCache(string? accessTokenMinutes = "15")
    {
        _config.Setup(c => c["Jwt:AccessTokenExpiryMinutes"]).Returns(accessTokenMinutes);
        return new UserBanCache(_redis.Object, _config.Object, NullLogger<UserBanCache>.Instance);
    }

    [Theory]
    [InlineData("15")]
    [InlineData("30")]
    public async Task MarkBanned_SetsKey_WithTtlFromConfiguredAccessTokenLifetimePlusMargin(string minutes)
    {
        await CreateCache(minutes).MarkBannedAsync("u1");

        var expected = TimeSpan.FromMinutes(int.Parse(minutes)) + UserBanCache.Margin;
        _redis.Verify(r => r.SetAsync("banned-user:u1", It.IsAny<string>(), expected), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("0")]
    public async Task MarkBanned_InvalidLifetimeConfig_Throws(string? minutes)
    {
        var act = () => CreateCache(minutes).MarkBannedAsync("u1");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task MarkBanned_RedisDown_DoesNotThrow()
    {
        _redis.Setup(r => r.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()))
            .ThrowsAsync(new Exception("redis down"));

        var act = () => CreateCache().MarkBannedAsync("u1");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Clear_RemovesKey()
    {
        await CreateCache().ClearAsync("u1");

        _redis.Verify(r => r.RemoveAsync("banned-user:u1"), Times.Once);
    }

    [Fact]
    public async Task IsBanned_KeyExists_ReturnsTrue()
    {
        _redis.Setup(r => r.ExistsAsync("banned-user:u1")).ReturnsAsync(true);

        (await CreateCache().IsBannedAsync("u1")).Should().BeTrue();
    }

    [Fact]
    public async Task IsBanned_KeyMissing_ReturnsFalse()
    {
        _redis.Setup(r => r.ExistsAsync("banned-user:u1")).ReturnsAsync(false);

        (await CreateCache().IsBannedAsync("u1")).Should().BeFalse();
    }

    [Fact]
    public async Task IsBanned_RedisDown_FailsOpen()
    {
        _redis.Setup(r => r.ExistsAsync(It.IsAny<string>())).ThrowsAsync(new Exception("redis down"));

        (await CreateCache().IsBannedAsync("u1")).Should().BeFalse();
    }
}
