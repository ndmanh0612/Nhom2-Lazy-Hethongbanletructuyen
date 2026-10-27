using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhoneStore.Modules.Order.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderFulfillmentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Carrier",
                schema: "ordering",
                table: "orders",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CartId",
                schema: "ordering",
                table: "orders",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CollectedAmount",
                schema: "ordering",
                table: "orders",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CollectedAt",
                schema: "ordering",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrackingNumber",
                schema: "ordering",
                table: "orders",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_CartId",
                schema: "ordering",
                table: "orders",
                column: "CartId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_CartId",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "Carrier",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "CartId",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "CollectedAmount",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "CollectedAt",
                schema: "ordering",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "TrackingNumber",
                schema: "ordering",
                table: "orders");
        }
    }
}
