using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceTrackingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddTimetableFeatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "EndTime",
                table: "AttendanceSessions",
                type: "time",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsReplacement",
                table: "AttendanceSessions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<TimeSpan>(
                name: "StartTime",
                table: "AttendanceSessions",
                type: "time",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PublicHolidays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    HolidayName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PublicHolidays", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PublicHolidays");

            migrationBuilder.DropColumn(
                name: "EndTime",
                table: "AttendanceSessions");

            migrationBuilder.DropColumn(
                name: "IsReplacement",
                table: "AttendanceSessions");

            migrationBuilder.DropColumn(
                name: "StartTime",
                table: "AttendanceSessions");
        }
    }
}
