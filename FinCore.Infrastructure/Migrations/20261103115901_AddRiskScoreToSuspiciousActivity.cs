using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinCore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRiskScoreToSuspiciousActivity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SuspiciousActivities_TransferId",
                table: "SuspiciousActivities");

            migrationBuilder.AddColumn<string>(
                name: "RiskLevel",
                table: "SuspiciousActivities",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "RiskScore",
                table: "SuspiciousActivities",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_SuspiciousActivities_TransferId",
                table: "SuspiciousActivities",
                column: "TransferId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_SuspiciousActivities_TransferId",
                table: "SuspiciousActivities");

            migrationBuilder.DropColumn(
                name: "RiskLevel",
                table: "SuspiciousActivities");

            migrationBuilder.DropColumn(
                name: "RiskScore",
                table: "SuspiciousActivities");

            migrationBuilder.CreateIndex(
                name: "IX_SuspiciousActivities_TransferId",
                table: "SuspiciousActivities",
                column: "TransferId");
        }
    }
}
