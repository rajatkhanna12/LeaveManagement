using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeaveManagement.Migrations
{
    /// <inheritdoc />
    public partial class Addtable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SalaryAdjustments",
                columns: table => new
                {
                    AdjustmentID = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeID = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    Year = table.Column<int>(type: "int", nullable: false),
                    LeavesTaken = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FreeLeavesUsed = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaidLeavesDeducted = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FreeLeavesRemaining = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OneDaySalaryValue = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDeduction = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    FinalSalary = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsApproved = table.Column<bool>(type: "bit", nullable: false),
                    ApprovedOn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApprovedBy = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SalaryAdjustments", x => x.AdjustmentID);
                    table.ForeignKey(
                        name: "FK_SalaryAdjustments_AspNetUsers_EmployeeID",
                        column: x => x.EmployeeID,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SalaryAdjustments_EmployeeID",
                table: "SalaryAdjustments",
                column: "EmployeeID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SalaryAdjustments");
        }
    }
}
