using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MiniErpApi.Migrations
{
    /// <inheritdoc />
    public partial class AddNotasFiscais : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NotasFiscais",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EntregaId = table.Column<int>(type: "integer", nullable: false),
                    ChaveAcesso = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: false),
                    Numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Serie = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    DataEmissao = table.Column<DateOnly>(type: "date", nullable: true),
                    EmitenteCnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    EmitenteNome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DestinatarioDocumento = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    DestinatarioNome = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ValorTotal = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    LidaPorIa = table.Column<bool>(type: "boolean", nullable: false),
                    CriadaEm = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Arquivo = table.Column<byte[]>(type: "bytea", nullable: true),
                    ArquivoTipo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ArquivoNome = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SessaoId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotasFiscais", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NotasFiscais_Entregas_EntregaId",
                        column: x => x.EntregaId,
                        principalTable: "Entregas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ItensNota",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NotaFiscalId = table.Column<int>(type: "integer", nullable: false),
                    Descricao = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Quantidade = table.Column<decimal>(type: "numeric(12,3)", precision: 12, scale: 3, nullable: false),
                    ValorUnitario = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    ValorTotal = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    SessaoId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensNota", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensNota_NotasFiscais_NotaFiscalId",
                        column: x => x.NotaFiscalId,
                        principalTable: "NotasFiscais",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ItensNota_NotaFiscalId",
                table: "ItensNota",
                column: "NotaFiscalId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscais_EntregaId",
                table: "NotasFiscais",
                column: "EntregaId");

            migrationBuilder.CreateIndex(
                name: "IX_NotasFiscais_SessaoId",
                table: "NotasFiscais",
                column: "SessaoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ItensNota");

            migrationBuilder.DropTable(
                name: "NotasFiscais");
        }
    }
}
