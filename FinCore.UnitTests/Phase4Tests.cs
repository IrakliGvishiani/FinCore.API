using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.Audit;
using FinCore.Application.Models.BankAccountDtos;
using FinCore.Application.Models.VirtualCardDtos;
using FinCore.Application.Services;
using FinCore.Domain.DomainExceptions;
using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using FinCore.Infrastructure.Persistence;
using FinCore.Tests.Support;
using FluentAssertions;
using Mapster;
using MapsterMapper;
using Moq;

namespace FinCore.Tests.Phase4;

public class PaymentReceiptTests
{
    private static (SimulatedPayment Payment, VirtualCard Card, BankAccount Source) Converted(SimulatedPaymentStatus status = SimulatedPaymentStatus.Completed)
    {
        var h = new PaymentHarness(allowAutoConversion: true);
        var p = SimulatedPayment.Create(h.Card, h.Gel, CurrencyCode.USD, 100m, "Demo Shop", null, "r1", new DateTime(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc));
        p.ApplyPricing(271.37m, 0.37037037m, 1.36m, 270m, FakeNbgRates.RateDate);
        if (status == SimulatedPaymentStatus.Completed)
            p.Settle(h.Gel,
                h.LedgerAccounts.Items.Single(a => a.AccountCode == LedgerAccountCodes.CardSettlement(CurrencyCode.USD)),
                h.LedgerAccounts.Items.Single(a => a.AccountCode == LedgerAccountCodes.Fx(CurrencyCode.GEL)),
                h.LedgerAccounts.Items.Single(a => a.AccountCode == LedgerAccountCodes.Fx(CurrencyCode.USD)),
                h.LedgerAccounts.Items.Single(a => a.AccountCode == LedgerAccountCodes.Fee(CurrencyCode.GEL)));
        else
            p.Fail(PaymentDeclineReason.InsufficientFunds);
        return (p, h.Card, h.Gel);
    }

    [Fact]
    public void Converted_ShowsMerchantAmount_ChargedAmountAndAccount_RateWithDate_Fee()
    {
        var (p, card, gel) = Converted();

        var lines = PaymentReceiptLines.Build(p, card, gel).ToDictionary(l => l.Label, l => l.Value);

        PaymentReceiptLines.Headline(p).Should().Be("−100.00 USD");
        lines["Merchant amount"].Should().Be("100.00 USD");
        lines["Charged amount"].Should().Be("271.37 GEL");
        lines["Charged from account"].Should().Be(gel.AccountNumber);
        lines["Exchange rate"].Should().Be("1 GEL = 0.37037037 USD");
        lines["Rate date"].Should().Be("20 Nov 2026");
        lines["Conversion fee"].Should().Be("1.36 GEL (included in the charged amount)");
    }

    [Fact]
    public void WithoutConversion_RowsAreExactlyTheOldOnes()
    {
        var h = new PaymentHarness();
        var p = SimulatedPayment.Create(h.Card, h.Gel, CurrencyCode.GEL, 25m, "Demo Shop", "Order 1", "r2", new DateTime(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc));
        p.ApplyPricing(25m, 1m, 0m, 25m, null);
        p.Fail(PaymentDeclineReason.InsufficientFunds);

        PaymentReceiptLines.Build(p, h.Card, h.Gel).Select(l => l.Label).Should().Equal(
            "Reference", "Merchant", "Card", "Currency", "Date", "Description", "Failure reason");
    }

    [Fact]
    public void FailedConversion_ShowsNoConversionRows_BecauseNothingWasCharged()
    {
        var (p, card, gel) = Converted(SimulatedPaymentStatus.Failed);

        PaymentReceiptLines.Build(p, card, gel).Select(l => l.Label)
            .Should().NotContain(new[] { "Charged amount", "Exchange rate", "Conversion fee" })
            .And.Contain("Failure reason");
    }

    [Fact]
    public void Pdf_IsGenerated_ForConvertedAndPlainPayments()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var (p, card, gel) = Converted();

        var pdf = ReceiptService.RenderPaymentReceipt(p, card, gel);

        pdf.Length.Should().BeGreaterThan(1000);
        System.Text.Encoding.ASCII.GetString(pdf, 0, 5).Should().Be("%PDF-");
    }
}

public class PaymentAuditDetailsTests
{
    [Fact]
    public async Task ConvertedPayment_AuditHasMerchantAmountChargedRateFeeLimitAndSourceAccount()
    {
        var h = new PaymentHarness(allowAutoConversion: true);
        string? details = null;
        h.Audit.Setup(a => a.RecordAsync(It.IsAny<string?>(), AuditActions.CardPaymentCompleted, AuditEntities.VirtualCard,
                "7", It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string?, string, string, string?, string?, CancellationToken>((_, _, _, _, d, _) => details = d)
            .Returns(Task.CompletedTask);

        await h.Service().PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(100m, CurrencyCode.USD));

        details.Should().Be(
            $"merchantAmount=100.00 USD; charged=271.37 GEL; rate=0.37037037; fee=1.36 GEL; limitAmount=270.00 GEL; " +
            $"sourceAccount={h.Gel.AccountNumber}; merchant=Demo Shop; reason=");
    }

    [Fact]
    public async Task DeclineBeforePricing_AuditSaysNotApplicable()
    {
        var h = new PaymentHarness();
        string? details = null;
        h.Audit.Setup(a => a.RecordAsync(It.IsAny<string?>(), AuditActions.CardPaymentDeclined, It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string?, string, string, string?, string?, CancellationToken>((_, _, _, _, d, _) => details = d)
            .Returns(Task.CompletedTask);

        await h.Service().PayAsync(PaymentHarness.Owner, PaymentHarness.NewShape(10m, CurrencyCode.USD));

        details.Should().Be(
            $"merchantAmount=10.00 USD; charged=n/a; rate=n/a; fee=n/a; limitAmount=n/a; " +
            $"sourceAccount={h.Gel.AccountNumber}; merchant=Demo Shop; reason=AutoConversionNotAllowed");
    }

    [Fact]
    public async Task OldShape_AuditUsesIdentityPricing()
    {
        var h = new PaymentHarness();
        string? details = null;
        h.Audit.Setup(a => a.RecordAsync(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback<string?, string, string, string?, string?, CancellationToken>((_, _, _, _, d, _) => details = d)
            .Returns(Task.CompletedTask);

        await h.Service().PayAsync(PaymentHarness.Owner, PaymentHarness.OldShape(25m));

        details.Should().StartWith("merchantAmount=25.00 GEL; charged=25.00 GEL; rate=1; fee=0.00 GEL; limitAmount=25.00 GEL;");
    }

    [Fact]
    public async Task Replay_WritesNoAuditRow()
    {
        var h = new PaymentHarness();
        var service = h.Service();
        await service.PayAsync(PaymentHarness.Owner, PaymentHarness.OldShape(25m));
        await service.PayAsync(PaymentHarness.Owner, PaymentHarness.OldShape(25m));

        h.Audit.Verify(a => a.RecordAsync(It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class AutoConversionToggleTests
{
    private readonly PaymentHarness _h = new();
    private readonly Mock<IAuditService> _audit = new();

    private VirtualCardService Service()
    {
        var config = new TypeAdapterConfig();
        new FinCore.Application.Mapping.MappingConfiguration().Register(config);
        return new VirtualCardService(_h.Cards, _h.Accounts, _h.Uow, _h.Payments, new Mapper(config), _audit.Object);
    }

    [Fact]
    public async Task Enable_UpdatesCard_Audits_ReturnsDto()
    {
        var dto = await Service().SetAutoConversionAsync(7, PaymentHarness.Owner, new SetAutoConversionDto { Enabled = true });

        dto.AllowAutoConversion.Should().BeTrue();
        dto.Id.Should().Be(7);
        _h.Card.AllowAutoConversion.Should().BeTrue();
        _h.Uow.Saves.Should().Be(1);
        _audit.Verify(a => a.RecordAsync(PaymentHarness.Owner, AuditActions.CardAutoConversionChanged, AuditEntities.VirtualCard,
            "7", "allowAutoConversion=true; previous=false", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Disable_Audits()
    {
        _h.Card.SetAutoConversion(true);

        await Service().SetAutoConversionAsync(7, PaymentHarness.Owner, new SetAutoConversionDto { Enabled = false });

        _audit.Verify(a => a.RecordAsync(PaymentHarness.Owner, AuditActions.CardAutoConversionChanged, AuditEntities.VirtualCard,
            "7", "allowAutoConversion=false; previous=true", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MissingEnabled_400()
    {
        await Service().Invoking(s => s.SetAutoConversionAsync(7, PaymentHarness.Owner, new SetAutoConversionDto()))
            .Should().ThrowAsync<BadRequestException>();
        _h.Uow.Saves.Should().Be(0);
    }

    [Fact]
    public async Task SomeoneElsesCard_404()
    {
        await Service().Invoking(s => s.SetAutoConversionAsync(7, PaymentHarness.Stranger, new SetAutoConversionDto { Enabled = true }))
            .Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ClosedCard_DomainException400_NotAudited()
    {
        _h.Card.Close();

        await Service().Invoking(s => s.SetAutoConversionAsync(7, PaymentHarness.Owner, new SetAutoConversionDto { Enabled = true }))
            .Should().ThrowAsync<DomainException>();
        _audit.Invocations.Should().BeEmpty();
    }

    [Fact]
    public void AuditAction_IsListedForTheAdminFilter()
    {
        AuditActions.All.Should().Contain("card.auto_conversion_changed");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Create_HonoursOptionalFlag_AndDtosExposeIt(bool flag)
    {
        var created = await Service().CreateAsync(PaymentHarness.Owner,
            new CreateVirtualCardDto { BankAccountId = _h.Usd.Id, Name = "Travel", DailyLimit = 500m, TransactionLimit = 100m, AllowAutoConversion = flag });

        created.AllowAutoConversion.Should().Be(flag);
        (await Service().GetByIdAsync(created.Id, PaymentHarness.Owner)).AllowAutoConversion.Should().Be(flag);
        (await Service().GetAllAsync(PaymentHarness.Owner)).Items.Single(c => c.Id == created.Id).AllowAutoConversion.Should().Be(flag);
    }

    [Fact]
    public async Task Create_WithoutFlag_DefaultsToOff()
    {
        var created = await Service().CreateAsync(PaymentHarness.Owner,
            new CreateVirtualCardDto { BankAccountId = _h.Usd.Id, Name = "Old client", DailyLimit = 500m, TransactionLimit = 100m });

        created.AllowAutoConversion.Should().BeFalse();
    }
}
