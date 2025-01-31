using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatientManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddFaceAdjustmentsToPatients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CheekAdjustment",
                table: "Patients",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ChinAdjustment",
                table: "Patients",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "NoseAdjustment",
                table: "Patients",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheekAdjustment",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "ChinAdjustment",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "NoseAdjustment",
                table: "Patients");
        }
    }
}
