using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeaveManagement.Migrations
{
    /// <inheritdoc />
    public partial class remove_coloum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaidLeave",
                table: "LeaveRequests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "PaidLeave",
                table: "LeaveRequests",
                type: "float",
                nullable: false,
                defaultValue: 0.0);
        }
    }
}
