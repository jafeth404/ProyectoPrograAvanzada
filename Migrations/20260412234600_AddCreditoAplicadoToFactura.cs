using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace proyectoprogra.Migrations
{
    /// <inheritdoc />
    public partial class AddCreditoAplicadoToFactura : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CreditoAplicado",
                table: "Facturas",
                type: "decimal(10,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreditoAplicado",
                table: "Facturas");
        }
    }
}
