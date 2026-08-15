using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrengthPlanner.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Blocks of a long-term plan used to be named "&lt;plan&gt; — blok N (&lt;template&gt;)".
    /// The generator now writes a plain hyphen; this brings names already in the database
    /// in line so the same plan does not render two different dashes across blocks.
    /// </summary>
    public partial class NormalizeGeneratedBlockNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """UPDATE "Mesocycles" SET "Name" = REPLACE("Name", '—', '-') WHERE "Name" LIKE '%—%';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Deliberately empty. Turning hyphens back into em dashes would also hit every
            // hyphen a user typed into a plan name, so the rollback leaves the text alone.
        }
    }
}
