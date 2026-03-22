using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StlExplorerServer.Migrations
{
    /// <inheritdoc />
    public partial class AddCheminDossier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CheminDossier",
                table: "Modeles",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheminDossier",
                table: "Modeles");
        }
    }
}
