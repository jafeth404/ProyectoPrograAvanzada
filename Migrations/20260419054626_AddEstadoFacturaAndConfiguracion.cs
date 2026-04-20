using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace proyectoprogra.Migrations
{
    /// <inheritdoc />
    public partial class AddEstadoFacturaAndConfiguracion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Estado",
                table: "Facturas",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ConfiguracionSistema",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TipoCargoDelivery = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CargoDelivery = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracionSistema", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracionSistema");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "Facturas");
        }
    }
}
