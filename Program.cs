using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using proyectoprogra.Data;
using proyectoprogra.Models;
using Fido2NetLib;
using proyectoprogra.Services;

var builder = WebApplication.CreateBuilder(args);

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

// 🔥 JWT AUTHENTICATION
var jwtSecret = builder.Configuration["Jwt:Secret"]!;
var jwtIssuer  = builder.Configuration["Jwt:Issuer"]!;
var jwtAudience = builder.Configuration["Jwt:Audience"]!;

builder.Services.AddAuthentication()
    .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtIssuer,
            ValidAudience            = jwtAudience,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

// 🔥 SWAGGER WITH JWT BEARER
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title   = "Restaurante Digital API",
        Version = "v1",
        Description = "REST API para el sistema de restaurante digital"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.ApiKey,
        Scheme       = "Bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Ingrese: Bearer {token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// 🔥 AUTORIZACIÓN GLOBAL (solo MVC, las API usan [Authorize(AuthenticationSchemes=...)])
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AuthorizeFilter());
});

builder.Services.AddRazorPages();

// 🔥 EMAIL (para forgot password)
builder.Services.AddTransient<EmailSender>();
builder.Services.AddTransient<IEmailSender>(sp => sp.GetRequiredService<EmailSender>());

// 🔥 PDF (cross-platform via QuestPDF)
builder.Services.AddSingleton<FacturaPdfService>();

// 🔥 IN-MEMORY CACHE (used by API list endpoints)
builder.Services.AddMemoryCache();

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

// 🔥 SWAGGER (available in all environments for demo/grading purposes)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Restaurante Digital API v1");
    c.RoutePrefix = "swagger";
});

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