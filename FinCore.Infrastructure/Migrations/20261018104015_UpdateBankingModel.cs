using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinCore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateBankingModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transfers_IdempotencyKey",
                table: "Transfers");

            migrationBuilder.AddColumn<string>(
                name: "InitiatedByUserId",
                table: "Transfers",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_InitiatedByUserId_IdempotencyKey",
                table: "Transfers",
                columns: new[] { "InitiatedByUserId", "IdempotencyKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transfers_InitiatedByUserId_IdempotencyKey",
                table: "Transfers");

            migrationBuilder.DropColumn(
                name: "InitiatedByUserId",
                table: "Transfers");

            migrationBuilder.CreateIndex(
                name: "IX_Transfers_IdempotencyKey",
                table: "Transfers",
                column: "IdempotencyKey",
                unique: true);
        }
    }
}
