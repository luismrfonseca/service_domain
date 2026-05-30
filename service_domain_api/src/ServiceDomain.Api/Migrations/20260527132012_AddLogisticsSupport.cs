using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ServiceDomain.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLogisticsSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Encomendas_DocumentoNo",
                table: "Encomendas");

            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                table: "Encomendas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Tipo",
                table: "Encomendas",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Localizacao",
                table: "EncomendaLinhas",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentLineId",
                table: "EncomendaLinhas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Encomendas_DocumentoNo_Tipo",
                table: "Encomendas",
                columns: new[] { "DocumentoNo", "Tipo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Encomendas_ParentId",
                table: "Encomendas",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_EncomendaLinhas_ParentLineId",
                table: "EncomendaLinhas",
                column: "ParentLineId");

            migrationBuilder.AddForeignKey(
                name: "FK_EncomendaLinhas_EncomendaLinhas_ParentLineId",
                table: "EncomendaLinhas",
                column: "ParentLineId",
                principalTable: "EncomendaLinhas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Encomendas_Encomendas_ParentId",
                table: "Encomendas",
                column: "ParentId",
                principalTable: "Encomendas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EncomendaLinhas_EncomendaLinhas_ParentLineId",
                table: "EncomendaLinhas");

            migrationBuilder.DropForeignKey(
                name: "FK_Encomendas_Encomendas_ParentId",
                table: "Encomendas");

            migrationBuilder.DropIndex(
                name: "IX_Encomendas_DocumentoNo_Tipo",
                table: "Encomendas");

            migrationBuilder.DropIndex(
                name: "IX_Encomendas_ParentId",
                table: "Encomendas");

            migrationBuilder.DropIndex(
                name: "IX_EncomendaLinhas_ParentLineId",
                table: "EncomendaLinhas");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "Encomendas");

            migrationBuilder.DropColumn(
                name: "Tipo",
                table: "Encomendas");

            migrationBuilder.DropColumn(
                name: "Localizacao",
                table: "EncomendaLinhas");

            migrationBuilder.DropColumn(
                name: "ParentLineId",
                table: "EncomendaLinhas");

            migrationBuilder.CreateIndex(
                name: "IX_Encomendas_DocumentoNo",
                table: "Encomendas",
                column: "DocumentoNo",
                unique: true);
        }
    }
}
