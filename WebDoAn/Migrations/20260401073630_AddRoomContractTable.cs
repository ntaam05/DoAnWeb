using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebDoAn.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomContractTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RoomContracts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoomPostId = table.Column<int>(type: "int", nullable: false),
                    LandlordEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContractContent = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TenantFrontIdCardUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantBackIdCardUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantFaceImageUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TenantSignatureUrl = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OtpCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OtpExpiry = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DocumentHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SignedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomContracts", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoomContracts");
        }
    }
}
