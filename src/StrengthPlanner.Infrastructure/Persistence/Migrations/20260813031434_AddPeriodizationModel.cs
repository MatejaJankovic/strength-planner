using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrengthPlanner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPeriodizationModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PeriodizationModel",
                table: "Mesocycles",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Flat");

            migrationBuilder.AddColumn<string>(
                name: "PeriodizationModel",
                table: "MacrocycleBlocks",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "Flat");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PeriodizationModel",
                table: "Mesocycles");

            migrationBuilder.DropColumn(
                name: "PeriodizationModel",
                table: "MacrocycleBlocks");
        }
    }
}
