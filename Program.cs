using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.UI.Services;
using proyectoprogra.Data;
using proyectoprogra.Models;
using DinkToPdf;
using DinkToPdf.Contracts;
using Fido2NetLib;
using proyectoprogra.Services;

var builder = WebApplication.CreateBuilder(args);
var context = new CustomAssemblyLoadContext();
var path = Path.Combine(AppContext.BaseDirectory, "libwkhtmltox.dll");

context.LoadUnmanagedLibrary(path);

builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));

// 🔥 DB
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// 🔥 IDENTITY + ROLES
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// 🔥 AUTORIZACIÓN GLOBAL
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AuthorizeFilter());
});

builder.Services.AddRazorPages();

// 🔥 EMAIL (para forgot password)
builder.Services.AddTransient<IEmailSender, EmailSender>();

builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));

// 🔥 SESSION (required by Fido2 challenge storage)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout        = TimeSpan.FromMinutes(10);
    options.Cookie.HttpOnly    = true;
    options.Cookie.IsEssential = true;
});

// 🔥 HACIENDA API
builder.Services.AddHttpClient<HaciendaApiService>(client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["HaciendaApi:BaseUrl"] ?? "https://api.hacienda.go.cr");
    client.DefaultRequestHeaders.Add("Accept", "application/json");
    client.Timeout = TimeSpan.FromSeconds(15);
});

// 🔥 FIDO2 / PASSKEYS
builder.Services.AddFido2(options =>
{
    options.ServerDomain           = builder.Configuration["Fido2:ServerDomain"]!;
    options.ServerName             = builder.Configuration["Fido2:ServerName"]!;
    options.Origins                = builder.Configuration
                                        .GetSection("Fido2:Origins")
                                        .Get<HashSet<string>>()!;
    options.TimestampDriftTolerance = builder.Configuration
                                        .GetValue<int>("Fido2:TimestampDriftTolerance");
});

var app = builder.Build();

// 🔥 PIPELINE
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseSession();          // 👈 must be before UseRouting (Fido2 challenge storage)

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// 🔥 ROUTES
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();


// 🔥 SEED: ROLES + ADMIN
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

    // 🔥 ROLES
    string[] roles = { "Administrador", "Usuario", "Contabilidad", "Salonero", "Cocina", "Cajero" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    // 🔥 ADMIN DEFAULT (IMPORTANTE PARA PROBAR)
    var adminEmail = "admin@admin.com";
    var adminPassword = "Admin123!";

    var admin = await userManager.FindByEmailAsync(adminEmail);

    if (admin == null)
    {
        admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            EmailConfirmed = true,

            Identificacion = "000000000",
            NombreCompleto = "Administrador",
            Genero = "No especifica",
             TipoTarjeta = "VISA",
            Ultimos4Tarjeta = "0000-0000-0000-0000"
        };

        var result = await userManager.CreateAsync(admin, adminPassword);

        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, "Administrador");
        }
    }
}

app.Run();