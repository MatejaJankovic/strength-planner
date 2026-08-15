using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StrengthPlanner.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPrescribedSets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PrescribedSets",
                table: "ExercisePlans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Kolona se dodaje sa nulom, a ona je za zatecene planove pogresna vrednost, ne
            // samo prazna: prozor u kome balansiranje volumena sme da se krece racuna se
            // oko propisa, pa bi propis nula svaki zatecen plan spustio na dve serije pri
            // prvom preracunu. Blokovi napravljeni pre ove izmene nisu ni balansirani, pa
            // im je predlog jos uvek jednak propisu — to je tacna vrednost za sidro.
            migrationBuilder.Sql("""
                UPDATE "ExercisePlans" SET "PrescribedSets" = "TargetSets";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrescribedSets",
                table: "ExercisePlans");
        }
    }
}
