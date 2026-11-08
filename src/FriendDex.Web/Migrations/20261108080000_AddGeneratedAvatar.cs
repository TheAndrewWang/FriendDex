using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FriendDex.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddGeneratedAvatar : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "Avatar",
                table: "Friends",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AvatarContentType",
                table: "Friends",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AvatarGeneratedAt",
                table: "Friends",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Avatar",
                table: "Friends");

            migrationBuilder.DropColumn(
                name: "AvatarContentType",
                table: "Friends");

            migrationBuilder.DropColumn(
                name: "AvatarGeneratedAt",
                table: "Friends");
        }
    }
}
