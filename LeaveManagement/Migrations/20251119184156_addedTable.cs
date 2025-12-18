using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeaveManagement.Migrations
{
    /// <inheritdoc />
    public partial class addedTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LeaveAdjustmentHistories",
                columns: table => new
                {
                    HistoryID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeaveType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FreeLeavesLeft = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaidLeaves = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveAdjustmentHistories", x => x.HistoryID);
                    table.ForeignKey(
                        name: "FK_LeaveAdjustmentHistories_AspNetUsers_EmployeeID",
                        column: x => x.EmployeeID,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeaveAdjustmentHistories_EmployeeID",
                table: "LeaveAdjustmentHistories",
                column: "EmployeeID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaveAdjustmentHistories");
        }
    }
}
