using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeaveManagement.Migrations
{
    /// <inheritdoc />
    public partial class AddtableHoliday : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Name",
                table: "Holidays",
                newName: "HolidayType");

            migrationBuilder.RenameColumn(
                name: "IsRecurringAnnually",
                table: "Holidays",
                newName: "IsActive");

            migrationBuilder.RenameColumn(
                name: "Date",
                table: "Holidays",
                newName: "HolidayDate");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Holidays",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CreatedBy",
                table: "Holidays",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedOn",
                table: "Holidays",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "HolidayName",
                table: "Holidays",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Month",
                table: "Holidays",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Year",
                table: "Holidays",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedBy",
                table: "Holidays");

            migrationBuilder.DropColumn(
                name: "CreatedOn",
                table: "Holidays");

            migrationBuilder.DropColumn(
                name: "HolidayName",
                table: "Holidays");

            migrationBuilder.DropColumn(
                name: "Month",
                table: "Holidays");

            migrationBuilder.DropColumn(
                name: "Year",
                table: "Holidays");

            migrationBuilder.RenameColumn(
                name: "IsActive",
                table: "Holidays",
                newName: "IsRecurringAnnually");

            migrationBuilder.RenameColumn(
                name: "HolidayType",
                table: "Holidays",
                newName: "Name");

            migrationBuilder.RenameColumn(
                name: "HolidayDate",
                table: "Holidays",
                newName: "Date");

            migrationBuilder.AlterColumn<string>(
                name: "Description",
                table: "Holidays",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }
    }
}
