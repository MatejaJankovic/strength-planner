using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrengthPlanner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPerceivedFatigue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PerceivedFatigue",
                table: "WorkoutSessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_WorkoutSessions_PerceivedFatigueRange",
                table: "WorkoutSessions",
                sql: "\"PerceivedFatigue\" IS NULL OR \"PerceivedFatigue\" BETWEEN 1 AND 5");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_WorkoutSessions_PerceivedFatigueRange",
                table: "WorkoutSessions");

            migrationBuilder.DropColumn(
                name: "PerceivedFatigue",
                table: "WorkoutSessions");
        }
    }
}
