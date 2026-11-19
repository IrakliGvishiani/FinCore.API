using FinCore.Application.Contracts.Persistence;
using FinCore.Application.Contracts.Services;
using FinCore.Application.Models.Audit;
using FinCore.Application.Services;
using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using FluentAssertions;
using MapsterMapper;
using Moq;
using System.Linq.Expressions;

namespace FinCore.Tests.Services;

public class AccountFreezeAuditTests
{
    private const string AccountNumber = "GE00TEST0000000000002";

    private readonly Mock<IRepositoryBase<BankAccount>> _accounts = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IAuditService> _audit = new();

    private AccountService CreateService() => new(
        _accounts.Object,
        _uow.Object,
        new Mock<IMapper>().Object,
        new Mock<IRepositoryBase<LedgerTransaction>>().Object,
        new Mock<IRepositoryBase<LedgerAccount>>().Object,
        _audit.Object);

    private void AccountLookupReturns(BankAccount account) =>
        _accounts.Setup(x => x.GetAsync(
                It.IsAny<Expression<Func<BankAccount, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<Func<IQueryable<BankAccount>, IQueryable<BankAccount>>[]>()))
            .ReturnsAsync(account);

    [Fact]
    public async Task Freeze_RecordsAdminAsActor_AccountAsEntity_OwnerInDetails()
    {
        var account = BankAccount.Create(AccountNumber, "owner-1", CurrencyCode.GEL, "Main");
        AccountLookupReturns(account);

        await CreateService().FreezeAccountAsync(AccountNumber, "admin-1");

        account.Status.Should().Be(AccountStatus.Frozen);
        _audit.Verify(a => a.RecordAsync("admin-1", AuditActions.AccountFrozen, AuditEntities.BankAccount,
            AccountNumber, "ownerUserId=owner-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Unfreeze_RecordsAdminAsActor_AccountAsEntity_OwnerInDetails()
    {
        var account = BankAccount.Create(AccountNumber, "owner-1", CurrencyCode.GEL, "Main");
        account.Freeze();
        AccountLookupReturns(account);

        await CreateService().UnfreezeAccountAsync(AccountNumber, "admin-1");

        account.Status.Should().Be(AccountStatus.Active);
        _audit.Verify(a => a.RecordAsync("admin-1", AuditActions.AccountUnfrozen, AuditEntities.BankAccount,
            AccountNumber, "ownerUserId=owner-1", It.IsAny<CancellationToken>()), Times.Once);
    }
}
