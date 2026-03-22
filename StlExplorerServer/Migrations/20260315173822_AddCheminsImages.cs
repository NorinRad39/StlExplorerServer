using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StlExplorerServer.Migrations
{
    /// <inheritdoc />
    public partial class AddCheminsImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CheminsImages",
                table: "Modeles",
                type: "longtext",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheminsImages",
                table: "Modeles");
        }
    }
}
