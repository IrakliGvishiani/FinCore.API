using FinCore.Application.Contracts.Persistence;
using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace FinCore.Infrastructure.Persistence
{
    public class ReceiptService : IReceiptService
    {
        private readonly IRepositoryBase<SimulatedPayment> _payments;
        private readonly IRepositoryBase<VirtualCard> _cards;
        private readonly IRepositoryBase<CurrencyConversion> _conversions;
        private readonly IRepositoryBase<Transfer> _transfers;


        public ReceiptService(
            IRepositoryBase<SimulatedPayment> repositoryBase,
            IRepositoryBase<VirtualCard> repositoryBase1,
            IRepositoryBase<CurrencyConversion> conversion,
            IRepositoryBase<Transfer> repositoryBase2
            )
        {
            _payments = repositoryBase;
            _cards = repositoryBase1;
            _conversions = conversion;
            _transfers = repositoryBase2;
        }

        public async Task<byte[]> GeneratePaymentReceiptAsync(
            string userId,
            int paymentId,
            CancellationToken ct = default)
        {
            var payment = await _payments.GetAsync(
                x => x.Id == paymentId,
                tracking: false,
                cancellationToken: ct
            ) ?? throw new NotFoundException("Payment not found.");

            var card = await _cards.GetAsync(
                x => x.Id == payment.VirtualCardId &&
                     x.UserId == userId,
                tracking: false,
                cancellationToken: ct
            ) ?? throw new NotFoundException("Payment not found.");

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Column(column =>
                    {
                        column.Item()
                            .Text("FinCore")
                            .FontSize(26)
                            .Bold()
                            .FontColor(Colors.Blue.Darken2);

                        column.Item()
                            .Text("DIGITAL BANKING")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken1);

                        column.Item()
                            .PaddingTop(15)
                            .LineHorizontal(1)
                            .LineColor(Colors.Grey.Lighten2);
                    });

                    page.Content().PaddingVertical(25).Column(column =>
                    {
                        column.Spacing(12);

                        column.Item()
                            .AlignCenter()
                            .Text("PAYMENT RECEIPT")
                            .FontSize(18)
                            .Bold();

                        column.Item()
                            .AlignCenter()
                            .Text(payment.Status.ToString().ToUpperInvariant())
                            .FontSize(12)
                            .Bold()
                            .FontColor(
                                payment.Status == SimulatedPaymentStatus.Completed
                                    ? Colors.Green.Darken2
                                    : Colors.Red.Darken2);

                        column.Item()
                            .PaddingVertical(15)
                            .AlignCenter()
                            .Text($"−{payment.Amount:N2} {payment.Currency}")
                            .FontSize(24)
                            .Bold();

                        column.Item().LineHorizontal(1)
                            .LineColor(Colors.Grey.Lighten2);

                        AddDetail(column, "Reference", payment.Reference);
                        AddDetail(column, "Merchant", payment.Merchant);
                        AddDetail(column, "Card", $"•••• {card.Last4Digits}");
                        AddDetail(column, "Currency", payment.Currency.ToString());
                        AddDetail(
                            column,
                            "Date",
                            payment.CreatedAt.ToString(
                                "dd MMM yyyy, HH:mm:ss",
                                CultureInfo.InvariantCulture) + " UTC");

                        AddDetail(
                            column,
                            "Description",
                            payment.Description ?? "N/A");

                        if (payment.FailureReason is not null)
                        {
                            AddDetail(
                                column,
                                "Failure reason",
                                payment.FailureReason.ToString()!);
                        }

                        column.Item()
                            .PaddingTop(15)
                            .LineHorizontal(1)
                            .LineColor(Colors.Grey.Lighten2);
                    });

                    page.Footer().AlignCenter().Text(
                        "This is a digitally generated transaction receipt from FinCore.")
                        .FontSize(9)
                        .FontColor(Colors.Grey.Darken1);
                });
            }).GeneratePdf();
        }

        public async Task<byte[]> GenerateCurrencyConversionReceiptAsync(
            string userId,
            int conversionId,
            CancellationToken ct = default)
        {
            var conversion = await _conversions.GetAsync(
                x => x.Id == conversionId &&
                     x.InitiatedByUserId == userId,
                tracking: false,
                cancellationToken: ct,
                query => query.Include(x => x.FromAccount),
                query => query.Include(x => x.ToAccount)
            ) ?? throw new NotFoundException("Conversion not found.");

            return Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Column(column =>
                    {
                        column.Item()
                            .Text("FinCore")
                            .FontSize(26)
                            .Bold()
                            .FontColor(Colors.Blue.Darken2);

                        column.Item()
                            .Text("DIGITAL BANKING")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken1);

                        column.Item()
                            .PaddingTop(15)
                            .LineHorizontal(1)
                            .LineColor(Colors.Grey.Lighten2);
                    });

                    page.Content().PaddingVertical(25).Column(column =>
                    {
                        column.Spacing(12);

                        column.Item()
                            .AlignCenter()
                            .Text("CURRENCY CONVERSION RECEIPT")
                            .FontSize(18)
                            .Bold();

                        column.Item()
                            .AlignCenter()
                            .Text("COMPLETED")
                            .FontSize(12)
                            .Bold()
                            .FontColor(Colors.Green.Darken2);

                        column.Item()
                            .PaddingVertical(15)
                            .AlignCenter()
                            .Text(
                                $"{conversion.ToAmount:N2} {conversion.ToCurrency}")
                            .FontSize(24)
                            .Bold()
                            .FontColor(Colors.Green.Darken2);

                        column.Item()
                            .LineHorizontal(1)
                            .LineColor(Colors.Grey.Lighten2);

                        AddDetail(column, "Reference", conversion.Reference);

                        AddDetail(
                            column,
                            "From Account",
                            conversion.FromAccount.AccountNumber);

                        AddDetail(
                            column,
                            "To Account",
                            conversion.ToAccount.AccountNumber);

                        AddDetail(
                            column,
                            "From Amount",
                            $"{conversion.FromAmount:N2} {conversion.FromCurrency}");

                        AddDetail(
                            column,
                            "Fee Rate",
                            conversion.FeeRate.ToString("P2", CultureInfo.InvariantCulture));

                        AddDetail(
                            column,
                            "Fee Amount",
                            $"{conversion.FeeAmount:N2} {conversion.FromCurrency}");

                        AddDetail(
                            column,
                            "Net Amount",
                            $"{conversion.NetAmount:N2} {conversion.FromCurrency}");

                        AddDetail(
                            column,
                            "Exchange Rate",
                            conversion.Rate.ToString(
                                CultureInfo.InvariantCulture));

                        AddDetail(
                            column,
                            "Received Amount",
                            $"{conversion.ToAmount:N2} {conversion.ToCurrency}");

                        AddDetail(
                            column,
                            "Rate Date",
                            conversion.RateDate.ToString(
                                "dd MMM yyyy",
                                CultureInfo.InvariantCulture));

                        AddDetail(
                            column,
                            "Created At",
                            conversion.CreatedAt.ToString(
                                "dd MMM yyyy, HH:mm:ss",
                                CultureInfo.InvariantCulture) + " UTC");

                        column.Item()
                            .PaddingTop(15)
                            .LineHorizontal(1)
                            .LineColor(Colors.Grey.Lighten2);
                    });

                    page.Footer().AlignCenter().Text(
                        "This is a digitally generated transaction receipt from FinCore.")
                        .FontSize(9)
                        .FontColor(Colors.Grey.Darken1);
                });
            }).GeneratePdf();
        }

        public async Task<byte[]> GenerateTransferReceiptAsync(
            string userId,
            int transferId,
            CancellationToken ct = default)
        {
            var transfer = await _transfers.GetAsync(
                x => x.Id == transferId &&
                     (x.FromAccount.UserId == userId ||
                      x.ToAccount.UserId == userId),
                tracking: false,
                cancellationToken: ct,
                query => query.Include(x => x.FromAccount),
                query => query.Include(x => x.ToAccount)
            ) ?? throw new NotFoundException("Transfer not found.");

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header()
                        .Text("FinCore")
                        .FontSize(24)
                        .Bold()
                        .FontColor(Colors.Blue.Darken2);

                    page.Content().Column(column =>
                    {
                        column.Spacing(12);

                        column.Item()
                            .Text("Transfer Receipt")
                            .FontSize(20)
                            .Bold();

                        AddDetail(column, "Transfer ID", transfer.Id.ToString());
                        AddDetail(column, "Status", transfer.Status.ToString());
                        AddDetail(column, "From Account", transfer.FromAccount.AccountNumber);
                        AddDetail(column, "To Account", MaskAccountNumber(transfer.ToAccount.AccountNumber));
                        AddDetail(column, "Amount",
                            $"{transfer.Amount:N2} {transfer.Currency}");
                        AddDetail(column, "Description",
                            string.IsNullOrWhiteSpace(transfer.Description)
                                ? "N/A"
                                : transfer.Description);
                        AddDetail(column, "Created At",
                            transfer.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                        AddDetail(column, "Completed At",
                            transfer.CompletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "N/A");

                        if (!string.IsNullOrWhiteSpace(transfer.FailureReason))
                        {
                            AddDetail(column, "Failure Reason", transfer.FailureReason);
                        }
                    });

                    page.Footer()
                        .AlignCenter()
                        .Text("FinCore Digital Banking")
                        .FontSize(9)
                        .FontColor(Colors.Grey.Medium);
                });
            });

            return pdf.GeneratePdf();
        }
        private static void AddDetail(
                ColumnDescriptor column,
                string label,
                string value)
            {
                column.Item().Row(row =>
                {
                    row.RelativeItem()
                        .Text(label)
                        .FontColor(Colors.Grey.Darken1);

                    row.RelativeItem()
                        .AlignRight()
                        .Text(value)
                        .Bold();
                });
            }

        private static string MaskAccountNumber(string accountNumber)
        {
            if (string.IsNullOrWhiteSpace(accountNumber))
                return string.Empty;

            const int visibleDigits = 4;

            if (accountNumber.Length <= visibleDigits)
                return new string('*', accountNumber.Length);

            return new string('*', accountNumber.Length - visibleDigits)
                + accountNumber[^visibleDigits..];
        }

    }
}
