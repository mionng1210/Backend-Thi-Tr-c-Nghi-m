using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExamsService.Migrations
{
    /// <summary>
    /// Migration to add Certification Exam fields
    /// Auto-runs when ExamsService starts
    /// </summary>
    public partial class AddCertificationExamFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add SubjectId if not exists
            migrationBuilder.AddColumn<int>(
                name: "SubjectId",
                table: "Exams",
                type: "int",
                nullable: true);

            // Add ImageUrl
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Exams",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            // Add Price
            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "Exams",
                type: "decimal(18,2)",
                nullable: true);

            // Add OriginalPrice
            migrationBuilder.AddColumn<decimal>(
                name: "OriginalPrice",
                table: "Exams",
                type: "decimal(18,2)",
                nullable: true);

            // Add Level
            migrationBuilder.AddColumn<string>(
                name: "Level",
                table: "Exams",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // Add Difficulty
            migrationBuilder.AddColumn<string>(
                name: "Difficulty",
                table: "Exams",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // Add Provider
            migrationBuilder.AddColumn<string>(
                name: "Provider",
                table: "Exams",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            // Add FeaturesJson
            migrationBuilder.AddColumn<string>(
                name: "FeaturesJson",
                table: "Exams",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            // Add ValidPeriod
            migrationBuilder.AddColumn<string>(
                name: "ValidPeriod",
                table: "Exams",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // Create foreign key for SubjectId if Subjects table exists
            migrationBuilder.CreateIndex(
                name: "IX_Exams_SubjectId",
                table: "Exams",
                column: "SubjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_Exams_Subjects_SubjectId",
                table: "Exams",
                column: "SubjectId",
                principalTable: "Subjects",
                principalColumn: "SubjectId",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop foreign key
            migrationBuilder.DropForeignKey(
                name: "FK_Exams_Subjects_SubjectId",
                table: "Exams");

            // Drop index
            migrationBuilder.DropIndex(
                name: "IX_Exams_SubjectId",
                table: "Exams");

            // Drop columns
            migrationBuilder.DropColumn(name: "SubjectId", table: "Exams");
            migrationBuilder.DropColumn(name: "ImageUrl", table: "Exams");
            migrationBuilder.DropColumn(name: "Price", table: "Exams");
            migrationBuilder.DropColumn(name: "OriginalPrice", table: "Exams");
            migrationBuilder.DropColumn(name: "Level", table: "Exams");
            migrationBuilder.DropColumn(name: "Difficulty", table: "Exams");
            migrationBuilder.DropColumn(name: "Provider", table: "Exams");
            migrationBuilder.DropColumn(name: "FeaturesJson", table: "Exams");
            migrationBuilder.DropColumn(name: "ValidPeriod", table: "Exams");
        }
    }
}

