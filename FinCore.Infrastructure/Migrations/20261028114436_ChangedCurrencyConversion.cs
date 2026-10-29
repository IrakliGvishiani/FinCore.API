using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinCore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangedCurrencyConversion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CurrencyConversions_DifferentAccounts",
                table: "CurrencyConversions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CurrencyConversions_DifferentCurrencies",
                table: "CurrencyConversions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CurrencyConversions_FromAmount_Positive",
                table: "CurrencyConversions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CurrencyConversions_Rate_Positive",
                table: "CurrencyConversions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_CurrencyConversions_ToAmount_Positive",
                table: "CurrencyConversions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_CurrencyConversions_DifferentAccounts",
                table: "CurrencyConversions",
                sql: "[FromAccountId] <> [ToAccountId]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CurrencyConversions_DifferentCurrencies",
                table: "CurrencyConversions",
                sql: "[FromCurrency] <> [ToCurrency]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CurrencyConversions_FromAmount_Positive",
                table: "CurrencyConversions",
                sql: "[FromAmount] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CurrencyConversions_Rate_Positive",
                table: "CurrencyConversions",
                sql: "[Rate] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_CurrencyConversions_ToAmount_Positive",
                table: "CurrencyConversions",
                sql: "[ToAmount] > 0");
        }
    }
}
