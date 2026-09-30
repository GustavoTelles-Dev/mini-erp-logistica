using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErpApi.Migrations
{
    /// <inheritdoc />
    public partial class AddMotorista : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MotoristaId",
                table: "Entregas",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Motoristas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    Cnh = table.Column<string>(type: "TEXT", nullable: false),
                    Telefone = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Motoristas", x => x.Id);
                });

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

            migrationBuilder.DropTable(
                name: "Motoristas");

            migrationBuilder.DropIndex(
                name: "IX_Entregas_MotoristaId",
                table: "Entregas");

            migrationBuilder.DropColumn(
                name: "MotoristaId",
                table: "Entregas");
        }
    }
}
