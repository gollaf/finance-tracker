using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FinanceTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAccountOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "Transactions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "ImportJobs",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "OwnerId",
                table: "Accounts",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_OwnerId",
                table: "Transactions",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportJobs_OwnerId",
                table: "ImportJobs",
                column: "OwnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_OwnerId",
                table: "Accounts",
                column: "OwnerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_OwnerId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_ImportJobs_OwnerId",
                table: "ImportJobs");

            migrationBuilder.DropIndex(
                name: "IX_Accounts_OwnerId",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "ImportJobs");

            migrationBuilder.DropColumn(
                name: "OwnerId",
                table: "Accounts");
        }
    }
}
