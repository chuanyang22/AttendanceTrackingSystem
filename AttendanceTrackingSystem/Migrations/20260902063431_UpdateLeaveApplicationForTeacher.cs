using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AttendanceTrackingSystem.Migrations
{
    /// <inheritdoc />
    public partial class UpdateLeaveApplicationForTeacher : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LeaveApplications_Students_StudentId",
                table: "LeaveApplications");

            migrationBuilder.AlterColumn<int>(
                name: "StudentId",
                table: "LeaveApplications",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "ApplicantName",
                table: "LeaveApplications",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ApplicantRole",
                table: "LeaveApplications",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "UserId",
                table: "LeaveApplications",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveApplications_UserId",
                table: "LeaveApplications",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveApplications_Students_StudentId",
                table: "LeaveApplications",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "StudentId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveApplications_Users_UserId",
                table: "LeaveApplications",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LeaveApplications_Students_StudentId",
                table: "LeaveApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveApplications_Users_UserId",
                table: "LeaveApplications");

            migrationBuilder.DropIndex(
                name: "IX_LeaveApplications_UserId",
                table: "LeaveApplications");

            migrationBuilder.DropColumn(
                name: "ApplicantName",
                table: "LeaveApplications");

            migrationBuilder.DropColumn(
                name: "ApplicantRole",
                table: "LeaveApplications");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "LeaveApplications");

            migrationBuilder.AlterColumn<int>(
                name: "StudentId",
                table: "LeaveApplications",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveApplications_Students_StudentId",
                table: "LeaveApplications",
                column: "StudentId",
                principalTable: "Students",
                principalColumn: "StudentId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
