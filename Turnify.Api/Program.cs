using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Interfaces;
using Turnify.Api.Services;
using Turnify.Api.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.FileProviders;

// --- ALIAS DE SWAGGER (Se mantienen tus alias) ---
using SwaggerDocInfo = Microsoft.OpenApi.Models.OpenApiInfo;
using SwaggerSecurityScheme = Microsoft.OpenApi.Models.OpenApiSecurityScheme;
using SwaggerSecurityRequirement = Microsoft.OpenApi.Models.OpenApiSecurityRequirement;
using SwaggerReference = Microsoft.OpenApi.Models.OpenApiReference;
using SwaggerReferenceType = Microsoft.OpenApi.Models.ReferenceType;
using SwaggerSecurityType = Microsoft.OpenApi.Models.SecuritySchemeType;
using SwaggerLocation = Microsoft.OpenApi.Models.ParameterLocation;

var builder = WebApplication.CreateBuilder(args);

// 1. CONFIGURACIÓN DE SERVICIOS
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

builder.Services.AddEndpointsApiExplorer();

// --- 🌍 CONFIGURACIÓN DE MULTIDIOMA ---
var supportedCultures = new[] { "es", "en", "fr", "ja", "zh", "de" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture(supportedCultures[0])
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// --- 🛡️ CONFIGURACIÓN DE CORS (Abierto para desarrollo local/Docker) ---
builder.Services.AddCors(options => {
    options.AddPolicy("AllowTurnify", b => 
    {
        b.AllowAnyOrigin()   
         .AllowAnyMethod()   
         .AllowAnyHeader();  
    });
});

// CONFIGURACIÓN DE SWAGGER (Se mantiene tu seguridad JWT)
builder.Services.AddSwaggerGen(c => {
    c.SwaggerDoc("v1", new SwaggerDocInfo { Title = "Turnify API", Version = "v1" });
    c.CustomSchemaIds(type => type.ToString());
 
    var securityScheme = new SwaggerSecurityScheme {
        Name = "JWT Authentication",
        Description = "Ingresa: Bearer {tu_token}",
        In = SwaggerLocation.Header,
        Type = SwaggerSecurityType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Reference = new SwaggerReference {
            Id = JwtBearerDefaults.AuthenticationScheme,
            Type = SwaggerReferenceType.SecurityScheme
        }
    };

    c.AddSecurityDefinition(securityScheme.Reference.Id, securityScheme);
    c.AddSecurityRequirement(new SwaggerSecurityRequirement { { securityScheme, new string[] { } } });
});

// 2. AUTENTICACIÓN JWT
var key = Encoding.ASCII.GetBytes(builder.Configuration["Jwt:Key"] ?? "Llave_Super_Secreta_De_Respaldo_32_Chars");
builder.Services.AddAuthentication(x => {
    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(x => {
    x.RequireHttpsMetadata = false;
    x.SaveToken = true;
    x.TokenValidationParameters = new TokenValidationParameters {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ClockSkew = TimeSpan.Zero
    };
});

// --- 🛡️ BASE DE DATOS CON RESILIENCIA ---
builder.Services.AddDbContext<TurnifyDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlServerOptionsAction: sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 10, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
        }
    ));

builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<IClienteService, ClienteService>();
builder.Services.AddScoped<ICitaService, CitaService>();
builder.Services.AddScoped<IServicioService, ServicioService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

var app = builder.Build();

// 3. MIDDLEWARES (Orden Crítico)

app.UseSwagger();
app.UseSwaggerUI(c => {
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Turnify API v1");
    c.RoutePrefix = "swagger"; 
});

app.UseRequestLocalization(localizationOptions); 
app.UseMiddleware<ExceptionMiddleware>();
app.UseCors("AllowTurnify");

// --- 🏗️ SERVICIO DE ARCHIVOS ESTÁTICOS MEJORADO ---
// 🚩 Ajuste para Docker: Buscamos donde realmente esté el contenido
string rootPath = builder.Environment.ContentRootPath;
string frontendPath = Path.Combine(rootPath, "frontend");

// Si dentro de frontend existe 'dist', priorizamos esa
if (Directory.Exists(Path.Combine(frontendPath, "dist"))) {
    frontendPath = Path.Combine(frontendPath, "dist");
}

Console.WriteLine($"--- 🔍 RUTA FINAL DE ARCHIVOS: {frontendPath} ---");

if (Directory.Exists(frontendPath))
{
    // Habilitar login.html como página de inicio
    var fileOptions = new DefaultFilesOptions();
    fileOptions.DefaultFileNames.Clear();
    fileOptions.DefaultFileNames.Add("login.html");
    fileOptions.FileProvider = new PhysicalFileProvider(frontendPath);
    
    app.UseDefaultFiles(fileOptions);

    // Servir estáticos con mapeo directo
    app.UseStaticFiles(new StaticFileOptions { 
        FileProvider = new PhysicalFileProvider(frontendPath),
        RequestPath = "" // Permite acceder a /reportes.js directamente
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers(); 

// 🚩 Fallback: Si no es una ruta de API, manda al login
app.MapFallbackToFile("login.html", new StaticFileOptions {
    FileProvider = new PhysicalFileProvider(frontendPath)
});

app.Run();

public class Messages { }