using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentDrive.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "RentItems",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "longtext")
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_RentItems_CreatedAt",
                table: "RentItems",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RentItems_PricePerDay",
                table: "RentItems",
                column: "PricePerDay");

            migrationBuilder.CreateIndex(
                name: "IX_RentItems_Title_Description",
                table: "RentItems",
                columns: new[] { "Title", "Description" })
                .Annotation("MySql:FullTextIndex", true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RentItems_CreatedAt",
                table: "RentItems");

            migrationBuilder.DropIndex(
                name: "IX_RentItems_PricePerDay",
                table: "RentItems");

            migrationBuilder.DropIndex(
                name: "IX_RentItems_Title_Description",
                table: "RentItems");

            migrationBuilder.AlterColumn<string>(
                name: "Title",
                table: "RentItems",
                type: "longtext",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
