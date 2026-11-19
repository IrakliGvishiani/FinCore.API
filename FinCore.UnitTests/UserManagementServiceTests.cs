using FinCore.Application.Contracts.Persistence;
using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.Audit;
using FinCore.Application.Services;
using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using FinCore.Tests.Support;
using FluentAssertions;
using Moq;

namespace FinCore.Tests.Services;

public class UserManagementServiceTests
{
    private readonly Mock<IAuditService> _audit = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IUserBanCache> _banCache = new();

    private UserManagementService CreateService(TestIdentity identity) =>
        new(identity.Manager, _audit.Object, _uow.Object, _banCache.Object);

    private static ApplicationUser User(string id, string first, string last, bool banned = false) => new()
    {
        Id = id,
        UserName = id,
        Email = $"{id}@example.com",
        FirstName = first,
        LastName = last,
        PersonalNumber = "12345678901",
        IsAdminLocked = banned
    };

    // ---------- Ban / unban ----------

    [Fact]
    public async Task Ban_AuditsAdminAsActor_TargetAsEntity_AndMarksBannedInRedis()
    {
        var target = User("cust-1", "Nino", "Beridze");
        var identity = new TestIdentity(roles: new List<string> { "Customer" });
        identity.ReturnOnFindById(target, "cust-1");

        await CreateService(identity).LockUserAsync("cust-1", "admin-1");

        target.IsAdminLocked.Should().BeTrue();
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _banCache.Verify(b => b.MarkBannedAsync("cust-1"), Times.Once);
        _audit.Verify(a => a.LogAsync("admin-1", AuditActions.UserLocked, AuditEntities.User, "cust-1",
            It.Is<string>(d => d.Contains("Nino Beridze"))), Times.Once);
    }

    [Fact]
    public async Task Ban_AdminTarget_Throws_AndDoesNothing()
    {
        var target = User("adm-2", "Other", "Admin");
        var identity = new TestIdentity(roles: new List<string> { "Admin" });
        identity.ReturnOnFindById(target, "adm-2");

        var act = () => CreateService(identity).LockUserAsync("adm-2", "admin-1");

        await act.Should().ThrowAsync<BadRequestException>();
        target.IsAdminLocked.Should().BeFalse();
        _banCache.Verify(b => b.MarkBannedAsync(It.IsAny<string>()), Times.Never);
        _audit.Verify(a => a.LogAsync(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task Unban_AuditsAdminAsActor_TargetAsEntity_AndClearsRedisMarker()
    {
        var target = User("cust-1", "Nino", "Beridze", banned: true);
        var identity = new TestIdentity(roles: new List<string> { "Customer" });
        identity.ReturnOnFindById(target, "cust-1");

        await CreateService(identity).UnlockUserAsync("cust-1", "admin-1");

        target.IsAdminLocked.Should().BeFalse();
        _banCache.Verify(b => b.ClearAsync("cust-1"), Times.Once);
        _audit.Verify(a => a.LogAsync("admin-1", AuditActions.UserUnlocked, AuditEntities.User, "cust-1",
            It.IsAny<string>()), Times.Once);
    }

    // ---------- IsBanned mapping (EF InMemory) ----------

    [Fact]
    public async Task GetUsers_MapsIsBannedFromIsAdminLocked()
    {
        using var db = UsersDbContext.CreateEmpty();
        db.AppUsers.AddRange(
            User("a", "Ana", "Abashidze", banned: true),
            User("b", "Beka", "Bakradze", banned: false));
        await db.SaveChangesAsync();

        var identity = new TestIdentity(users: db.AppUsers);

        var (items, total) = await CreateService(identity).GetUsersAsync(null, 1, 20);

        total.Should().Be(2);
        items.Single(i => i.Id == "a").IsBanned.Should().BeTrue();
        items.Single(i => i.Id == "b").IsBanned.Should().BeFalse();
    }

    [Fact]
    public async Task GetUsers_PasswordLockoutAlone_IsNotBanned()
    {
        using var db = UsersDbContext.CreateEmpty();
        var lockedOut = User("c", "Gio", "Chkheidze");
        lockedOut.LockoutEnd = DateTimeOffset.UtcNow.AddMinutes(10);
        db.AppUsers.Add(lockedOut);
        await db.SaveChangesAsync();

        var identity = new TestIdentity(users: db.AppUsers);

        var (items, _) = await CreateService(identity).GetUsersAsync(null, 1, 20);

        items.Single().IsBanned.Should().BeFalse();
    }

    [Fact]
    public async Task GetUserById_MapsIsBannedFromIsAdminLocked()
    {
        using var db = UsersDbContext.CreateEmpty();
        var user = User("d", "Dato", "Dolidze", banned: true);
        db.AppUsers.Add(user);
        db.Set<BankAccount>().Add(BankAccount.Create("GE00TEST0000000000001", "d", CurrencyCode.GEL, "Main"));
        await db.SaveChangesAsync();

        var identity = new TestIdentity(users: db.AppUsers);

        var details = await CreateService(identity).GetUserByIdAsync("d");

        details.IsBanned.Should().BeTrue();
        details.BankAccounts.Should().HaveCount(1);
    }
}
