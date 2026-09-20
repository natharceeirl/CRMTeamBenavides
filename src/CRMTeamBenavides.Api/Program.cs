using System.Text;
using CRMTeamBenavides.Api.Configuration;
using CRMTeamBenavides.Api.Features.Auth;
using CRMTeamBenavides.Api.Features.CategoriasProducto;
using CRMTeamBenavides.Api.Features.Clientes;
using CRMTeamBenavides.Api.Features.Inventario;
using CRMTeamBenavides.Api.Features.OrdenesServicio;
using CRMTeamBenavides.Api.Features.Permisos;
using CRMTeamBenavides.Api.Features.Roles;
using CRMTeamBenavides.Api.Features.Usuarios;
using CRMTeamBenavides.Api.Features.Vehiculos;
using CRMTeamBenavides.Api.Features.Chatbot;
using CRMTeamBenavides.Api.Features.Dashboard;
using CRMTeamBenavides.Api.Features.Reportes;
using CRMTeamBenavides.Api.Features.Ventas;
using CRMTeamBenavides.Api.Features.Yamaha;
using CRMTeamBenavides.Api.Services;
using CRMTeamBenavides.Data;
using CRMTeamBenavides.Data.Seed;
using CRMTeamBenavides.Api.Middleware;
using CRMTeamBenavides.Domain.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDataProtection();

builder.Services
    .AddIdentityCore<Usuario>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services
    .AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection(JwtSettings.SectionName))
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.SecretKey),
        "JwtSettings:SecretKey es obligatorio.")
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.Issuer),
        "JwtSettings:Issuer es obligatorio.")
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.Audience),
        "JwtSettings:Audience es obligatorio.")
    .Validate(settings => settings.AccessTokenExpirationMinutes > 0,
        "JwtSettings:AccessTokenExpirationMinutes debe ser mayor que 0.")
    .Validate(settings => settings.RefreshTokenExpirationDays > 0,
        "JwtSettings:RefreshTokenExpirationDays debe ser mayor que 0.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services
    .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtSettings>>((jwtBearerOptions, jwtSettingsOptions) =>
    {
        var jwtSettings = jwtSettingsOptions.Value;

        jwtBearerOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IClienteService, ClienteService>();
builder.Services.AddScoped<IVehiculoService, VehiculoService>();
builder.Services.AddScoped<IRolService, RolService>();
builder.Services.AddScoped<IPermisoService, PermisoService>();
builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<IOrdenServicioService, OrdenServicioService>();
builder.Services.AddScoped<ICategoriaProductoService, CategoriaProductoService>();
builder.Services.AddScoped<IInventarioService, InventarioService>();
builder.Services.AddScoped<IVentaService, VentaService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IReporteService, ReporteService>();
builder.Services.AddScoped<IChatbotService, ChatbotService>();

// ---------------------------------------------------------------------------
// Yamaha API — infraestructura del conector
// ---------------------------------------------------------------------------
// BaseUrl, AuthHeaderName y AuthHeaderValue deben proveerse en User Secrets o
// variables de entorno. No incluir secretos en appsettings.json.
// El esquema de autenticación exacto (API Key, Bearer, Basic, etc.) debe
// confirmarse con la documentación oficial de Yamaha antes de definir esos valores.
// Las operaciones de negocio reales se agregarán cuando el cliente entregue
// documentación, credenciales y permisos oficiales de Yamaha.
// ---------------------------------------------------------------------------
builder.Services
    .AddOptions<YamahaSettings>()
    .Bind(builder.Configuration.GetSection(YamahaSettings.SectionName));

builder.Services
    .AddHttpClient<IYamahaApiClient, YamahaApiClient>((serviceProvider, client) =>
    {
        var settings = serviceProvider
            .GetRequiredService<IOptions<YamahaSettings>>()
            .Value;

        if (!string.IsNullOrWhiteSpace(settings.BaseUrl))
            client.BaseAddress = new Uri(settings.BaseUrl);

        client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds > 0 ? settings.TimeoutSeconds : 30);

        if (!string.IsNullOrWhiteSpace(settings.AuthHeaderName) &&
            !string.IsNullOrWhiteSpace(settings.AuthHeaderValue))
        {
            client.DefaultRequestHeaders.Add(settings.AuthHeaderName, settings.AuthHeaderValue);
        }
    });

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

// Configure the HTTP request pipeline and initial seeding.
using (var seedScope = app.Services.CreateScope())
{
    var dbContext = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await RolSeeder.SeedAsync(dbContext);

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        await DevelopmentUserSeeder.SeedAsync(seedScope.ServiceProvider, app.Configuration);
        await FaqSeeder.SeedAsync(dbContext);
    }
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapClienteEndpoints();
app.MapVehiculoEndpoints();
app.MapRolEndpoints();
app.MapPermisoEndpoints();
app.MapUsuarioEndpoints();
app.MapOrdenServicioEndpoints();
app.MapCategoriaProductoEndpoints();
app.MapInventarioEndpoints();
app.MapVentaEndpoints();
app.MapDashboardEndpoints();
app.MapReporteEndpoints();
app.MapChatbotEndpoints();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}