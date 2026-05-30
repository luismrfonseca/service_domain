using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDomain.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Clientes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    No = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NomeFiscal = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PhcStamp = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clientes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Encomendas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentoNo = table.Column<int>(type: "int", nullable: false),
                    ClienteNo = table.Column<int>(type: "int", nullable: false),
                    Data = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PhcStamp = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Encomendas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Localizacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Armazem = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PhcStamp = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Localizacoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Lotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoteCodigo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Ref = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PhcStamp = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lotes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Produtos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ref = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Designacao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    PhcStamp = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Produtos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Stocks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ref = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    LoteCodigo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Armazem = table.Column<int>(type: "int", nullable: false),
                    Localizacao = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Quantidade = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    PhcStamp = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stocks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncInbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PhcStamp = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncInbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncOutbox",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncOutbox", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EncomendaLinhas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EncomendaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Ref = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Designacao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Quantidade = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Preco = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    Lote = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PhcStamp = table.Column<string>(type: "nvarchar(25)", maxLength: 25, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncomendaLinhas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EncomendaLinhas_Encomendas_EncomendaId",
                        column: x => x.EncomendaId,
                        principalTable: "Encomendas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_No",
                table: "Clientes",
                column: "No",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Clientes_PhcStamp",
                table: "Clientes",
                column: "PhcStamp",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EncomendaLinhas_EncomendaId",
                table: "EncomendaLinhas",
                column: "EncomendaId");

            migrationBuilder.CreateIndex(
                name: "IX_EncomendaLinhas_PhcStamp",
                table: "EncomendaLinhas",
                column: "PhcStamp");

            migrationBuilder.CreateIndex(
                name: "IX_Encomendas_DocumentoNo",
                table: "Encomendas",
                column: "DocumentoNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Encomendas_PhcStamp",
                table: "Encomendas",
                column: "PhcStamp");

            migrationBuilder.CreateIndex(
                name: "IX_Encomendas_Status",
                table: "Encomendas",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Localizacoes_Armazem",
                table: "Localizacoes",
                column: "Armazem",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Localizacoes_PhcStamp",
                table: "Localizacoes",
                column: "PhcStamp",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lotes_PhcStamp",
                table: "Lotes",
                column: "PhcStamp",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lotes_Ref_LoteCodigo",
                table: "Lotes",
                columns: new[] { "Ref", "LoteCodigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Produtos_PhcStamp",
                table: "Produtos",
                column: "PhcStamp",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Produtos_Ref",
                table: "Produtos",
                column: "Ref",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_PhcStamp",
                table: "Stocks",
                column: "PhcStamp");

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_Ref_LoteCodigo_Armazem_Localizacao",
                table: "Stocks",
                columns: new[] { "Ref", "LoteCodigo", "Armazem", "Localizacao" },
                unique: true,
                filter: "[LoteCodigo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SyncInbox_Status_CreatedAt",
                table: "SyncInbox",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncOutbox_Status_CreatedAt",
                table: "SyncOutbox",
                columns: new[] { "Status", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Clientes");

            migrationBuilder.DropTable(
                name: "EncomendaLinhas");

            migrationBuilder.DropTable(
                name: "Localizacoes");

            migrationBuilder.DropTable(
                name: "Lotes");

            migrationBuilder.DropTable(
                name: "Produtos");

            migrationBuilder.DropTable(
                name: "Stocks");

            migrationBuilder.DropTable(
                name: "SyncInbox");

            migrationBuilder.DropTable(
                name: "SyncOutbox");

            migrationBuilder.DropTable(
                name: "Encomendas");
        }
    }
}
