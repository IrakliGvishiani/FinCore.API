using FinCore.Application.Contracts.Persistence;
using FinCore.Application.Contracts.Services;
using FinCore.Application.Models.BankAccountDtos;
using FinCore.Application.Models.ExchangeDtos;
using Microsoft.Extensions.Options;
using FinCore.Application.Models.VirtualCardDtos;
using FinCore.Application.Services;
using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using FluentAssertions;
using Moq;
using System.Data;
using System.Linq.Expressions;

namespace FinCore.Tests.Services;

public class SimulatedPaymentServiceTests
{
    private readonly Mock<IRepositoryBase<SimulatedPayment>> _payments = new();
    private readonly Mock<IRepositoryBase<VirtualCard>> _cards = new();
    private readonly Mock<IRepositoryBase<BankAccount>> _accounts = new();
    private readonly Mock<IRepositoryBase<LedgerAccount>> _ledgerAccounts = new();
    private readonly Mock<IRepositoryBase<LedgerTransaction>> _ledgerTransactions = new();

    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<IAuditService> _audit = new();

    private SimulatedPaymentService CreateService()
    {
        return new SimulatedPaymentService(
            _payments.Object,
            _cards.Object,
            _accounts.Object,
            _ledgerAccounts.Object,
            _ledgerTransactions.Object,
            _unitOfWork.Object,
            _audit.Object,
            new FxPricingService(new Mock<IExchangeRateProvider>(MockBehavior.Strict).Object, Options.Create(new ConversionOptions()))
        );
    }

    [Fact]
    public async Task SuccessfulPayment_ShouldCompletePaymentAndDebitAccount()
    {
        
        const string userId = "test-user";

        
        var account = BankAccount.Create(
            "TEST-ACCOUNT-001",
            userId,
            CurrencyCode.USD
        );

        SetId(account, 1);
        account.Credit(100m);

        
        var card = VirtualCard.Create(
            userId,
            account,
            "Test Card",
            "1234",
            dailyLimit: 200m,
            transactionLimit: 50m,
            DateTime.UtcNow
        );

        SetId(card, 1);

        
        var settlementAccount = LedgerAccount.Create(
            LedgerAccountCodes.CardSettlement(CurrencyCode.USD),
            "USD Card Settlement",
            CurrencyCode.USD
        );

        SetId(settlementAccount, 10);

        
        var model = new CreatePaymentDto
        {
            VirtualCardId = card.Id,
            Amount = 25m,
            Merchant = "Test Store",
            Description = "Test payment",
            IdempotencyKey = "payment-test-001"
        };

        SetupUnitOfWork();

        

        _cards
            .Setup(x => x.GetAsync(
                It.IsAny<Expression<Func<VirtualCard, bool>>>(),
                true,
                It.IsAny<CancellationToken>(),
                It.IsAny<Func<IQueryable<VirtualCard>, IQueryable<VirtualCard>>[]>()))
            .ReturnsAsync(card);

        _payments
            .Setup(x => x.GetAsync(
                It.IsAny<Expression<Func<SimulatedPayment, bool>>>(),
                false,
                It.IsAny<CancellationToken>(),
                It.IsAny<Func<IQueryable<SimulatedPayment>, IQueryable<SimulatedPayment>>[]>()))
            .ReturnsAsync((SimulatedPayment?)null);

        _accounts
            .Setup(x => x.GetAsync(
                It.IsAny<Expression<Func<BankAccount, bool>>>(),
                true,
                It.IsAny<CancellationToken>(),
                It.IsAny<Func<IQueryable<BankAccount>, IQueryable<BankAccount>>[]>()))
            .ReturnsAsync(account);

        _payments
            .Setup(x => x.GetAllAsync(
                It.IsAny<Expression<Func<SimulatedPayment, bool>>>(),
                null,
                null,
                null,
                true,
                It.IsAny<CancellationToken>(),
                false,
                It.IsAny<Func<IQueryable<SimulatedPayment>, IQueryable<SimulatedPayment>>[]>()))
            .ReturnsAsync((Enumerable.Empty<SimulatedPayment>(), 0));

        _ledgerAccounts
            .Setup(x => x.GetAsync(
                It.IsAny<Expression<Func<LedgerAccount, bool>>>(),
                false,
                It.IsAny<CancellationToken>(),
                It.IsAny<Func<IQueryable<LedgerAccount>, IQueryable<LedgerAccount>>[]>()))
            .ReturnsAsync(settlementAccount);

        _ledgerTransactions
            .Setup(x => x.AddAsync(
                It.IsAny<LedgerTransaction>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _payments
            .Setup(x => x.AddAsync(
                It.IsAny<SimulatedPayment>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _audit
            .Setup(x => x.RecordAsync(
                It.IsAny<string?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var service = CreateService();

        // Act
        var result = await service.PayAsync(userId, model);

        // Assert
        result.Status.Should().Be(SimulatedPaymentStatus.Completed);

        result.Amount.Should().Be(25m);
        result.Currency.Should().Be(CurrencyCode.USD);
        result.Merchant.Should().Be("Test Store");
        result.Last4Digits.Should().Be("1234");

        account.Balance.Should().Be(75m);

        _ledgerTransactions.Verify(
            x => x.AddAsync(
                It.IsAny<LedgerTransaction>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _payments.Verify(
            x => x.AddAsync(
                It.IsAny<SimulatedPayment>(),
                It.IsAny<CancellationToken>()),
            Times.Once);

        _unitOfWork.Verify(
            x => x.CommitTransactionAsync(
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static void SetId<T>(T entity, int id)
    {
        typeof(T).GetProperty("Id")!.SetValue(entity, id);
    }

    private void SetupUnitOfWork()
    {
        _unitOfWork
            .Setup(x => x.BeginTransactionAsync(
                It.IsAny<IsolationLevel>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWork
            .Setup(x => x.CommitTransactionAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWork
            .Setup(x => x.RollbackTransactionAsync(
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _unitOfWork
            .Setup(x => x.SaveChangesAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
    }
}