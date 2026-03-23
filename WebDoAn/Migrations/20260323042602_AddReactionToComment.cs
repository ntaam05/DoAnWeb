using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebDoAn.Migrations
{
    /// <inheritdoc />
    public partial class AddReactionToComment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Reaction",
                table: "RoomComments",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Reaction",
                table: "RoomComments");
        }
    }
}
