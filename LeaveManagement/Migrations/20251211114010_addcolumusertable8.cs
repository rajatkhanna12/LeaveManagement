using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeaveManagement.Migrations
{
    /// <inheritdoc />
    public partial class addcolumusertable8 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "UsedFreeLeaves",
                table: "AspNetUsers",
                newName: "FreeLeavesLeft");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "FreeLeavesLeft",
                table: "AspNetUsers",
                newName: "UsedFreeLeaves");
        }
    }
}
