using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDomain.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWmsOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DataUltimaContagem",
                table: "Stocks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataValidade",
                table: "Stocks",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClasseAbc",
                table: "Produtos",
                type: "nvarchar(1)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Gtin",
                table: "Produtos",
                type: "nvarchar(14)",
                maxLength: 14,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "PesoUnitarioKg",
                table: "Produtos",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "RequerCq",
                table: "Produtos",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "VolumeUnitarioM3",
                table: "Produtos",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Alveolo",
                table: "Localizacoes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Corredor",
                table: "Localizacoes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Estante",
                table: "Localizacoes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "MaxPesoKg",
                table: "Localizacoes",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MaxVolumeM3",
                table: "Localizacoes",
                type: "decimal(18,6)",
                precision: 18,
                scale: 6,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Prateleira",
                table: "Localizacoes",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Zona",
                table: "Localizacoes",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "OrdensContagem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoContagem = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SupervisorId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrdensContagem", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Rmas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RmaCodigo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    InvoiceRef = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ClienteNo = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rmas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LinhasContagem",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrdemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StockId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuantidadeSistema = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    QuantidadeContada1 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Operador1Id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    QuantidadeContada2 = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Operador2Id = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DataAprovacao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AjusteAplicado = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinhasContagem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LinhasContagem_OrdensContagem_OrdemId",
                        column: x => x.OrdemId,
                        principalTable: "OrdensContagem",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LinhasContagem_Stocks_StockId",
                        column: x => x.StockId,
                        principalTable: "Stocks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RmaLinhas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RmaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ref = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Quantidade = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Grading = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    DestinoLocalizacao = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RmaLinhas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RmaLinhas_Rmas_RmaId",
                        column: x => x.RmaId,
                        principalTable: "Rmas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LinhasContagem_OrdemId",
                table: "LinhasContagem",
                column: "OrdemId");

            migrationBuilder.CreateIndex(
                name: "IX_LinhasContagem_StockId",
                table: "LinhasContagem",
                column: "StockId");

            migrationBuilder.CreateIndex(
                name: "IX_OrdensContagem_Estado",
                table: "OrdensContagem",
                column: "Estado");

            migrationBuilder.CreateIndex(
                name: "IX_RmaLinhas_RmaId",
                table: "RmaLinhas",
                column: "RmaId");

            migrationBuilder.CreateIndex(
                name: "IX_Rmas_RmaCodigo",
                table: "Rmas",
                column: "RmaCodigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rmas_Status",
                table: "Rmas",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LinhasContagem");

            migrationBuilder.DropTable(
                name: "RmaLinhas");

            migrationBuilder.DropTable(
                name: "OrdensContagem");

            migrationBuilder.DropTable(
                name: "Rmas");

            migrationBuilder.DropColumn(
                name: "DataUltimaContagem",
                table: "Stocks");

            migrationBuilder.DropColumn(
                name: "DataValidade",
                table: "Stocks");

            migrationBuilder.DropColumn(
                name: "ClasseAbc",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "Gtin",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "PesoUnitarioKg",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "RequerCq",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "VolumeUnitarioM3",
                table: "Produtos");

            migrationBuilder.DropColumn(
                name: "Alveolo",
                table: "Localizacoes");

            migrationBuilder.DropColumn(
                name: "Corredor",
                table: "Localizacoes");

            migrationBuilder.DropColumn(
                name: "Estante",
                table: "Localizacoes");

            migrationBuilder.DropColumn(
                name: "MaxPesoKg",
                table: "Localizacoes");

            migrationBuilder.DropColumn(
                name: "MaxVolumeM3",
                table: "Localizacoes");

            migrationBuilder.DropColumn(
                name: "Prateleira",
                table: "Localizacoes");

            migrationBuilder.DropColumn(
                name: "Zona",
                table: "Localizacoes");
        }
    }
}
