using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PhoneStore.Modules.Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVariantImageAndOriginalPrice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                schema: "catalog",
                table: "product_variants",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "OriginalPrice",
                schema: "catalog",
                table: "product_variants",
                type: "bigint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                schema: "catalog",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "OriginalPrice",
                schema: "catalog",
                table: "product_variants");
        }
    }
}
