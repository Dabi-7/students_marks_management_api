using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarksApi.Migrations
{
    /// <inheritdoc />
    public partial class FixUserStudentRelationship : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_users_students_student_id",
                table: "users");

            migrationBuilder.AddForeignKey(
                name: "fk_users_students_student_id",
                table: "users",
                column: "student_id",
                principalTable: "students",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_users_students_student_id",
                table: "users");

            migrationBuilder.AddForeignKey(
                name: "fk_users_students_student_id",
                table: "users",
                column: "student_id",
                principalTable: "students",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
