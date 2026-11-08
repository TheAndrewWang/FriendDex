using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FriendDex.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoAndLimits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Photo",
                table: "Friends",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotoContentType",
                table: "Friends",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Photo",
                table: "Friends");

            migrationBuilder.DropColumn(
                name: "PhotoContentType",
                table: "Friends");
        }
    }
}
