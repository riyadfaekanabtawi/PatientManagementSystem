using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PatientManagementSystem.Migrations
{
    /// <inheritdoc />
    public partial class AddModel3DUrlToPatients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Model3DUrl",
                table: "Patients",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Model3DUrl",
                table: "Patients");
        }
    }
}
