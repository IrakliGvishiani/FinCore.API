using FinCore.Application.Contracts.Persistence;
using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.Audit;
using FinCore.Application.Models.AuthModels;
using FinCore.Application.Services;
using FinCore.Domain.Entities;
using FinCore.Tests.Support;
using FluentAssertions;
using MapsterMapper;
using Microsoft.Extensions.Configuration;
using Moq;

namespace FinCore.Tests.Services;

public class AuthServiceTests
{
    private const string InvalidRefreshMessage = "Invalid or expired refresh token.";

    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IJwtTokenGenerator> _jwt = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly Mock<IEmailService> _email = new();
    private readonly Mock<IRedisService> _redis = new();
    private readonly Mock<IConfiguration> _config = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly UsersDbContext _db = UsersDbContext.CreateEmpty();
    private readonly TestIdentity _identity;

    public AuthServiceTests()
    {
        _identity = new TestIdentity(users: _db.AppUsers);

        _config.Setup(c => c["Jwt:RefreshTokenExpiryDays"]).Returns("7");
        _config.Setup(c => c["AppSettings:FrontendBaseUrl"]).Returns("http://localhost:4200");

        _jwt.Setup(j => j.GenerateToken(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()))
            .Returns("new-access");
        _jwt.Setup(j => j.GenerateRefreshToken()).Returns("new-refresh");
    }

    private AuthService CreateService() => new(
        _uow.Object,
        _identity.Manager,
        roleManager: null!,
        _jwt.Object,
        _mapper.Object,
        _email.Object,
        _redis.Object,
        _config.Object,
        _audit.Object);

    private static ApplicationUser User(string id, bool banned = false) => new()
    {
        Id = id,
        UserName = id,
        Email = $"{id}@example.com",
        FirstName = "Test",
        LastName = "User",
        PersonalNumber = "12345678901",
        IsAdminLocked = banned
    };

    // ---------- Refresh ----------

    [Fact]
    public async Task Refresh_BannedUser_Throws400_ConsumesToken_AndIssuesNothing()
    {
        _redis.Setup(r => r.GetAndRemoveAsync("refresh-token:old")).ReturnsAsync("u1");
        _identity.ReturnOnFindById(User("u1", banned: true), "u1");

        var act = () => CreateService().RefreshTokenAsync("old");

        await act.Should().ThrowAsync<BadRequestException>().WithMessage(InvalidRefreshMessage);

        _redis.Verify(r => r.GetAndRemoveAsync("refresh-token:old"), Times.Once);
        _redis.Verify(r => r.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Never);
        _jwt.Verify(j => j.GenerateToken(It.IsAny<ApplicationUser>(), It.IsAny<IEnumerable<string>>()), Times.Never);
        _audit.Verify(a => a.LogAsync("u1", AuditActions.LoginFailed, AuditEntities.User, "u1",
            It.Is<string>(d => d.Contains("banned"))), Times.Once);
    }

    [Fact]
    public async Task Refresh_UnknownUser_Throws400_AndConsumesToken()
    {
        _redis.Setup(r => r.GetAndRemoveAsync("refresh-token:old")).ReturnsAsync("ghost");
        _identity.ReturnOnFindById(null, "ghost");

        var act = () => CreateService().RefreshTokenAsync("old");

        await act.Should().ThrowAsync<BadRequestException>().WithMessage(InvalidRefreshMessage);
        _redis.Verify(r => r.GetAndRemoveAsync("refresh-token:old"), Times.Once);
        _redis.Verify(r => r.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Never);
    }

    [Fact]
    public async Task Refresh_UnknownToken_Throws400()
    {
        _redis.Setup(r => r.GetAndRemoveAsync(It.IsAny<string>())).ReturnsAsync((string?)null);

        var act = () => CreateService().RefreshTokenAsync("nope");

        await act.Should().ThrowAsync<BadRequestException>().WithMessage(InvalidRefreshMessage);
    }

    [Fact]
    public async Task Refresh_ValidUser_IssuesNewPair_AndConsumesOldToken()
    {
        _redis.Setup(r => r.GetAndRemoveAsync("refresh-token:old")).ReturnsAsync("u1");
        _identity.ReturnOnFindById(User("u1"), "u1");

        var result = await CreateService().RefreshTokenAsync("old");

        result.AccessToken.Should().Be("new-access");
        result.RefreshToken.Should().Be("new-refresh");
        _redis.Verify(r => r.GetAndRemoveAsync("refresh-token:old"), Times.Once);
        _redis.Verify(r => r.SetAsync("refresh-token:new-refresh", "u1", TimeSpan.FromDays(7)), Times.Once);
    }

    [Fact]
    public async Task Refresh_SameTokenTwice_SecondCallFails()
    {
        // GETDEL returns the value once; the second read sees nothing.
        _redis.SetupSequence(r => r.GetAndRemoveAsync("refresh-token:old"))
            .ReturnsAsync("u1")
            .ReturnsAsync((string?)null);
        _identity.ReturnOnFindById(User("u1"), "u1");

        var service = CreateService();
        await service.RefreshTokenAsync("old");
        var second = () => service.RefreshTokenAsync("old");

        await second.Should().ThrowAsync<BadRequestException>().WithMessage(InvalidRefreshMessage);
        _redis.Verify(r => r.SetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>()), Times.Once);
    }

    // ---------- Register audit actor ----------

    private UserRegistrationDto RegistrationModel() => new()
    {
        FirstName = "New",
        LastName = "Person",
        Email = "new.person@example.com",
        PersonalNumber = "01234567890",
        PhoneNumber = "+995555123456",
        Password = "Str0ng!Passw0rd"
    };

    private ApplicationUser MapperReturnsNewUser()
    {
        var created = new ApplicationUser
        {
            UserName = "new.person",
            Email = "new.person@example.com",
            FirstName = "New",
            LastName = "Person",
            PersonalNumber = "01234567890"
        };
        _mapper.Setup(m => m.Map<ApplicationUser>(It.IsAny<object>())).Returns(created);
        return created;
    }

    [Fact]
    public async Task RegisterAdmin_AuditsActingAdmin_AndCreatedUserAsEntity()
    {
        var created = MapperReturnsNewUser();

        var id = await CreateService().RegisterAdminAsync(RegistrationModel(), "admin-1");

        id.Should().Be(created.Id);
        _audit.Verify(a => a.LogAsync("admin-1", AuditActions.Registered, AuditEntities.User, created.Id,
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RegisterManager_AuditsActingAdmin_AndCreatedUserAsEntity()
    {
        var created = MapperReturnsNewUser();

        await CreateService().RegisterManagerAsync(RegistrationModel(), "admin-1");

        _audit.Verify(a => a.LogAsync("admin-1", AuditActions.Registered, AuditEntities.User, created.Id,
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task RegisterUser_SelfRegistration_StillAuditsNewUserAsActor()
    {
        var created = MapperReturnsNewUser();

        await CreateService().RegisterUserAsync(RegistrationModel());

        _audit.Verify(a => a.LogAsync(created.Id, AuditActions.Registered, AuditEntities.User, created.Id,
            It.IsAny<string>()), Times.Once);
    }
}
