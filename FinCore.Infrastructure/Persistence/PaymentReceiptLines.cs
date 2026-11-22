using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using System.Globalization;

namespace FinCore.Infrastructure.Persistence
{
    public sealed record ReceiptLine(string Label, string Value);

    /// <summary>
    /// The label/value rows of a payment receipt, kept separate from the PDF layout so they can be tested.
    /// Without conversion the rows are exactly the ones the receipt always had. When a conversion happened
    /// (completed payment, charged currency differs from the merchant currency) the conversion rows are added:
    /// charged amount, charged account, exchange rate with its date, and the FX fee.
    /// </summary>
    public static class PaymentReceiptLines
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Headline amount: what the merchant received, as before.</summary>
        public static string Headline(SimulatedPayment p) => $"−{p.Amount.ToString("N2", Inv)} {p.Currency}";

        public static bool ShowsConversion(SimulatedPayment p) =>
            p.IsConversion && p.Status == SimulatedPaymentStatus.Completed && p.IsPriced;

        public static IReadOnlyList<ReceiptLine> Build(SimulatedPayment p, VirtualCard card, BankAccount? sourceAccount)
        {
            var lines = new List<ReceiptLine>
            {
                new("Reference", p.Reference),
                new("Merchant", p.Merchant),
                new("Card", $"•••• {card.Last4Digits}"),
                new("Currency", p.Currency.ToString()),
                new("Date", p.CreatedAt.ToString("dd MMM yyyy, HH:mm:ss", Inv) + " UTC"),
                new("Description", p.Description ?? "N/A"),
            };

            if (ShowsConversion(p))
            {
                lines.Add(new("Merchant amount", $"{p.Amount.ToString("N2", Inv)} {p.Currency}"));
                lines.Add(new("Charged amount", $"{p.ChargedAmount!.Value.ToString("N2", Inv)} {p.ChargedCurrency}"));
                lines.Add(new("Charged from account", sourceAccount?.AccountNumber ?? "N/A"));
                lines.Add(new("Exchange rate", $"1 {p.ChargedCurrency} = {p.ExchangeRate!.Value.ToString("0.########", Inv)} {p.Currency}"));
                lines.Add(new("Rate date", p.RateDate?.ToString("dd MMM yyyy", Inv) ?? "N/A"));
                lines.Add(new("Conversion fee", $"{p.FeeAmount!.Value.ToString("N2", Inv)} {p.ChargedCurrency} (included in the charged amount)"));
            }

            if (p.FailureReason is not null)
                lines.Add(new("Failure reason", p.FailureReason.ToString()!));

            return lines;
        }
    }
}
