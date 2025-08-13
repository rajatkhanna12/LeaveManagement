using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeaveManagement.Migrations
{
    /// <inheritdoc />
    public partial class addGuid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
         name: "UserId",
         table: "tblAttendances");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "tblAttendances",
                type: "uniqueidentifier",
                nullable: false,
                defaultValueSql: "NEWID()");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
         name: "UserId",
         table: "tblAttendances");

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "tblAttendances",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }
    }
}
