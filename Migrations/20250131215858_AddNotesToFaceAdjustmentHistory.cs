using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatientManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddNotesToFaceAdjustmentHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "FaceAdjustmentHistories",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notes",
                table: "FaceAdjustmentHistories");
        }
    }
}
