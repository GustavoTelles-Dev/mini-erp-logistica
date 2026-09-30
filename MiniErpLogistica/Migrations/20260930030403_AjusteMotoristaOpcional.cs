using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErpLogistica.Migrations
{
    /// <inheritdoc />
    public partial class AjusteMotoristaOpcional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MotoristaId",
                table: "Entregas",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Entregas_MotoristaId",
                table: "Entregas",
                column: "MotoristaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Entregas_Motoristas_MotoristaId",
                table: "Entregas",
                column: "MotoristaId",
                principalTable: "Motoristas",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Entregas_Motoristas_MotoristaId",
                table: "Entregas");

            migrationBuilder.DropIndex(
                name: "IX_Entregas_MotoristaId",
                table: "Entregas");

            migrationBuilder.DropColumn(
                name: "MotoristaId",
                table: "Entregas");
        }
    }
}
