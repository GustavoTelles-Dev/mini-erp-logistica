using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MiniErpApi.Migrations
{
    /// <inheritdoc />
    public partial class Seguranca : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ArquivoTamanho",
                table: "NotasFiscais",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Entregas",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArquivoTamanho",
                table: "NotasFiscais");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Entregas");
        }
    }
}
