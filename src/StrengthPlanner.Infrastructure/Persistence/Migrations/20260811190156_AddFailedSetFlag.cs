using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrengthPlanner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFailedSetFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFailure",
                table: "SetLogs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SetLogs_FailureHasNoRir",
                table: "SetLogs",
                sql: "NOT \"IsFailure\" OR \"Rir\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SetLogs_FailureHasNoRir",
                table: "SetLogs");

            migrationBuilder.DropColumn(
                name: "IsFailure",
                table: "SetLogs");
        }
    }
}
