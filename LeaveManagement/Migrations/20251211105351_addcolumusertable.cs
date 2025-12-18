using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeaveManagement.Migrations
{
    /// <inheritdoc />
    public partial class addcolumusertable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "UsedFreeLeaves",
                table: "AspNetUsers",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "YearlyFreeLeaves",
                table: "AspNetUsers",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UsedFreeLeaves",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "YearlyFreeLeaves",
                table: "AspNetUsers");
        }
    }
}
