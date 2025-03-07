using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StlExplorerServer.Migrations
{
    /// <inheritdoc />
    public partial class AddSujetsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Packets_Sujet_SujetID",
                table: "Packets");

            migrationBuilder.DropForeignKey(
                name: "FK_Sujet_Famille_FamilleID",
                table: "Sujet");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Sujet",
                table: "Sujet");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Famille",
                table: "Famille");

            migrationBuilder.RenameTable(
                name: "Sujet",
                newName: "Sujets");

            migrationBuilder.RenameTable(
                name: "Famille",
                newName: "Familles");

            migrationBuilder.RenameIndex(
                name: "IX_Sujet_FamilleID",
                table: "Sujets",
                newName: "IX_Sujets_FamilleID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Sujets",
                table: "Sujets",
                column: "SujetID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Familles",
                table: "Familles",
                column: "FamilleID");

            migrationBuilder.AddForeignKey(
                name: "FK_Packets_Sujets_SujetID",
                table: "Packets",
                column: "SujetID",
                principalTable: "Sujets",
                principalColumn: "SujetID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Sujets_Familles_FamilleID",
                table: "Sujets",
                column: "FamilleID",
                principalTable: "Familles",
                principalColumn: "FamilleID",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Packets_Sujets_SujetID",
                table: "Packets");

            migrationBuilder.DropForeignKey(
                name: "FK_Sujets_Familles_FamilleID",
                table: "Sujets");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Sujets",
                table: "Sujets");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Familles",
                table: "Familles");

            migrationBuilder.RenameTable(
                name: "Sujets",
                newName: "Sujet");

            migrationBuilder.RenameTable(
                name: "Familles",
                newName: "Famille");

            migrationBuilder.RenameIndex(
                name: "IX_Sujets_FamilleID",
                table: "Sujet",
                newName: "IX_Sujet_FamilleID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Sujet",
                table: "Sujet",
                column: "SujetID");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Famille",
                table: "Famille",
                column: "FamilleID");

            migrationBuilder.AddForeignKey(
                name: "FK_Packets_Sujet_SujetID",
                table: "Packets",
                column: "SujetID",
                principalTable: "Sujet",
                principalColumn: "SujetID",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Sujet_Famille_FamilleID",
                table: "Sujet",
                column: "FamilleID",
                principalTable: "Famille",
                principalColumn: "FamilleID",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
