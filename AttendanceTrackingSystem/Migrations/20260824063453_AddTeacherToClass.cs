using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceTrackingSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherToClass : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TeacherName",
                table: "SchoolClasses");

            migrationBuilder.AddColumn<int>(
                name: "TeacherId",
                table: "SchoolClasses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE SchoolClasses SET TeacherId = (SELECT ISNULL(MIN(UserId), 1) FROM Users);");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolClasses_TeacherId",
                table: "SchoolClasses",
                column: "TeacherId");

            migrationBuilder.AddForeignKey(
                name: "FK_SchoolClasses_Users_TeacherId",
                table: "SchoolClasses",
                column: "TeacherId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SchoolClasses_Users_TeacherId",
                table: "SchoolClasses");

            migrationBuilder.DropIndex(
                name: "IX_SchoolClasses_TeacherId",
                table: "SchoolClasses");

            migrationBuilder.DropColumn(
                name: "TeacherId",
                table: "SchoolClasses");

            migrationBuilder.AddColumn<string>(
                name: "TeacherName",
                table: "SchoolClasses",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }
    }
}
