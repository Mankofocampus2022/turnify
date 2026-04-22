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
using Microsoft.AspNetCore.Diagnostics;

// --- ALIAS DE SWAGGER ---
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

// --- 🛡️ CONFIGURACIÓN DE CORS ---
builder.Services.AddCors(options => {
    options.AddPolicy("AllowTurnify", b => 
    {
        b.AllowAnyOrigin()   
         .AllowAnyMethod()   
         .AllowAnyHeader();  
    });
});

// CONFIGURACIÓN DE SWAGGER
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

// --- 🛡️ CONFIGURACIÓN DE BASE DE DATOS CON RESILIENCIA ---
builder.Services.AddDbContext<TurnifyDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlServerOptionsAction: sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 10,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        }
    ));

builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<IClienteService, ClienteService>();
builder.Services.AddScoped<ICitaService, CitaService>();
builder.Services.AddScoped<IServicioService, ServicioService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

// --- 🏗️ CONSTRUCCIÓN DE LA APP ---
var app = builder.Build();

// 3. MIDDLEWARES (ORDEN SENIOR OBLIGATORIO)

app.UseSwagger();
app.UseSwaggerUI(c => {
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Turnify API v1");
    c.RoutePrefix = "swagger"; 
});

app.UseRequestLocalization(localizationOptions); 
app.UseMiddleware<ExceptionMiddleware>();
app.UseCors("AllowTurnify");

// --- 🛡️ BLOQUE DE ARCHIVOS ESTÁTICOS BLINDADO (Sincronizado con Docker) ---
// 🚩 CAMBIO CRÍTICO: Quitamos "dist" porque Docker ya mapea el contenido de dist en "frontend"
var frontendPath = Path.Combine(builder.Environment.ContentRootPath, "frontend");

Console.WriteLine($"--- 🔍 RUTA BUSCADA: {frontendPath} ---");

if (Directory.Exists(frontendPath))
{
    Console.WriteLine("--- ✅ CARPETA ENCONTRADA. ACTIVANDO FRONTEND ---");
    
    // Permitir archivos por defecto (login.html)
    app.UseDefaultFiles(new DefaultFilesOptions { 
        FileProvider = new PhysicalFileProvider(frontendPath),
        DefaultFileNames = new List<string> { "login.html" } // Blindaje extra
    });

    app.UseStaticFiles(new StaticFileOptions { 
        FileProvider = new PhysicalFileProvider(frontendPath),
        RequestPath = "" 
    });
}
else
{
    // Fallback por si corres local sin Docker
    string backupPath = Path.GetFullPath("frontend/dist");
    Console.WriteLine($"--- ❌ NO ENCONTRADA. INTENTANDO BACKUP: {backupPath} ---");
    
    if (Directory.Exists(backupPath)) {
        app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(backupPath) });
    }
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers(); 

app.Run();

public class Messages { }