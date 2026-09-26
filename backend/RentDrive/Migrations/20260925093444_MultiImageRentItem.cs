using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RentDrive.Migrations
{
    /// <inheritdoc />
    public partial class MultiImageRentItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ImageUrl",
                table: "RentItems",
                newName: "ImageUrls");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ImageUrls",
                table: "RentItems",
                newName: "ImageUrl");
        }
    }
}
