using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderFlow.Adapters.Outbound.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddIntegrationReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "payments",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalReference",
                table: "orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_payments_IdempotencyKey",
                table: "payments",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_orders_ExternalReference",
                table: "orders",
                column: "ExternalReference");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_payments_IdempotencyKey",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_orders_ExternalReference",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "ExternalReference",
                table: "orders");
        }
    }
}
