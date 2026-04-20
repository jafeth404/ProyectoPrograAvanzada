using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using proyectoprogra.Models.Entities;
using proyectoprogra.Models;
using Fido2NetLib.Objects;
namespace proyectoprogra.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Categoria> Categorias { get; set; }
        public DbSet<Producto> Productos { get; set; }
        public DbSet<Mesa> Mesas { get; set; }
        public DbSet<Pedido> Pedidos { get; set; }
        public DbSet<PedidoDetalle> PedidoDetalles { get; set; }
        public DbSet<Factura> Facturas { get; set; }
        public DbSet<FacturaDetalle> FacturaDetalles { get; set; }

        public DbSet<UsuarioExtra> UsuariosExtra { get; set; }

        public DbSet<FidoStoredCredential> FidoCredentials { get; set; }

        public DbSet<ConfiguracionSistema> ConfiguracionSistema { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Producto>()
                .Property(p => p.Precio)
                .HasColumnType("decimal(10,2)");

            modelBuilder.Entity<Producto>()
                .Property(p => p.CostoEmpaque)
                .HasColumnType("decimal(10,2)");

            modelBuilder.Entity<PedidoDetalle>()
                .Property(p => p.PrecioUnitario)
                .HasColumnType("decimal(10,2)");

            modelBuilder.Entity<Factura>()
                .Property(f => f.Subtotal)
                .HasColumnType("decimal(10,2)");

            modelBuilder.Entity<Factura>()
                .Property(f => f.Iva)
                .HasColumnType("decimal(10,2)");

            modelBuilder.Entity<Factura>()
                .Property(f => f.Total)
                .HasColumnType("decimal(10,2)");

            modelBuilder.Entity<Factura>()
                .Property(f => f.Propina)
                .HasColumnType("decimal(10,2)");

            modelBuilder.Entity<Factura>()
                .Property(f => f.CostoEmpaque)
                .HasColumnType("decimal(10,2)");

            modelBuilder.Entity<Factura>()
                .Property(f => f.CostoDelivery)
                .HasColumnType("decimal(10,2)");

            modelBuilder.Entity<Factura>()
                .Property(f => f.CreditoAplicado)
                .HasColumnType("decimal(10,2)");

            modelBuilder.Entity<FacturaDetalle>()
                .Property(f => f.PrecioUnitario)
                .HasColumnType("decimal(10,2)");

            modelBuilder.Entity<FacturaDetalle>()
                .Property(f => f.TotalLinea)
                .HasColumnType("decimal(10,2)");

            modelBuilder.Entity<FidoStoredCredential>()
                .HasIndex(f => f.CredentialIdBase64)
                .IsUnique();
        }
    }
}