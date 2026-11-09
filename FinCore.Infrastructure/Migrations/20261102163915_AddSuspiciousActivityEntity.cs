using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinCore.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSuspiciousActivityEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SuspiciousActivities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    TransferId = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SuspiciousActivities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SuspiciousActivities_Transfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "Transfers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SuspiciousActivities_Users_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SuspiciousActivities_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SuspiciousActivities_CreatedAt",
                table: "SuspiciousActivities",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SuspiciousActivities_ReviewedByUserId",
                table: "SuspiciousActivities",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SuspiciousActivities_Status",
                table: "SuspiciousActivities",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SuspiciousActivities_TransferId",
                table: "SuspiciousActivities",
                column: "TransferId");

            migrationBuilder.CreateIndex(
                name: "IX_SuspiciousActivities_UserId",
                table: "SuspiciousActivities",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SuspiciousActivities");
        }
    }
}
