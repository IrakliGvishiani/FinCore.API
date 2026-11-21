using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinCore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFxCardPayments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowAutoConversion",
                table: "VirtualCards",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAuthorizedAt",
                table: "VirtualCards",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "VirtualCards",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<decimal>(
                name: "ChargedAmount",
                table: "SimulatedPayments",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            // Added as NULL, backfilled below, then made NOT NULL.
            migrationBuilder.AddColumn<string>(
                name: "ChargedCurrency",
                table: "SimulatedPayments",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ExchangeRate",
                table: "SimulatedPayments",
                type: "decimal(18,8)",
                precision: 18,
                scale: 8,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FeeAmount",
                table: "SimulatedPayments",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LimitAmount",
                table: "SimulatedPayments",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RateDate",
                table: "SimulatedPayments",
                type: "datetime2",
                nullable: true);

            // Added as NULL, backfilled below, then made NOT NULL.
            migrationBuilder.AddColumn<int>(
                name: "SourceBankAccountId",
                table: "SimulatedPayments",
                type: "int",
                nullable: true);

            // Backfill existing payments. Before this feature every payment was charged from the card's
            // own account, in that account's currency, without conversion or fee, so:
            //   source = card's account, charged = amount, charged currency = currency,
            //   rate = 1, fee = 0, limit amount = amount, rate date = NULL (no rate was used).
            // Every payment has a card (non-null FK, Restrict), so the inner join covers every row;
            // if it ever did not, the NOT NULL changes below fail and the whole migration rolls back.
            // Wrapped in EXEC so it also works in a single-batch SQL script (`dotnet ef migrations script`),
            // where the new columns would not exist yet when the batch is compiled.
            migrationBuilder.Sql(@"
EXEC(N'
UPDATE p
SET p.SourceBankAccountId = c.BankAccountId,
    p.ChargedAmount       = p.Amount,
    p.ChargedCurrency     = p.Currency,
    p.ExchangeRate        = 1,
    p.FeeAmount           = 0,
    p.LimitAmount         = p.Amount
FROM SimulatedPayments p
INNER JOIN VirtualCards c ON c.Id = p.VirtualCardId
WHERE p.SourceBankAccountId IS NULL;');");

            migrationBuilder.AlterColumn<int>(
                name: "SourceBankAccountId",
                table: "SimulatedPayments",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ChargedCurrency",
                table: "SimulatedPayments",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(3)",
                oldMaxLength: 3,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SimulatedPayments_SourceBankAccountId",
                table: "SimulatedPayments",
                column: "SourceBankAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_SimulatedPayments_BankAccounts_SourceBankAccountId",
                table: "SimulatedPayments",
                column: "SourceBankAccountId",
                principalTable: "BankAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SimulatedPayments_BankAccounts_SourceBankAccountId",
                table: "SimulatedPayments");

            migrationBuilder.DropIndex(
                name: "IX_SimulatedPayments_SourceBankAccountId",
                table: "SimulatedPayments");

            migrationBuilder.DropColumn(
                name: "AllowAutoConversion",
                table: "VirtualCards");

            migrationBuilder.DropColumn(
                name: "LastAuthorizedAt",
                table: "VirtualCards");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "VirtualCards");

            migrationBuilder.DropColumn(
                name: "ChargedAmount",
                table: "SimulatedPayments");

            migrationBuilder.DropColumn(
                name: "ChargedCurrency",
                table: "SimulatedPayments");

            migrationBuilder.DropColumn(
                name: "ExchangeRate",
                table: "SimulatedPayments");

            migrationBuilder.DropColumn(
                name: "FeeAmount",
                table: "SimulatedPayments");

            migrationBuilder.DropColumn(
                name: "LimitAmount",
                table: "SimulatedPayments");

            migrationBuilder.DropColumn(
                name: "RateDate",
                table: "SimulatedPayments");

            migrationBuilder.DropColumn(
                name: "SourceBankAccountId",
                table: "SimulatedPayments");
        }
    }
}
