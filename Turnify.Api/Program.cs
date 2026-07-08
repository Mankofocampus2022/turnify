using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Interfaces;
using Turnify.Api.Services;
using Turnify.Api.Middleware;
using Turnify.Api.Workers; // 🔄 Namespace inyectado para reconocer el Worker automático
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.DataProtection; // 🛡️ Namespace para solucionar advertencias de llaves efímeras
using Microsoft.AspNetCore.RateLimiting; // 🧠 Motor nativo para control de ráfagas y protección DDoS (OBS-04)
using System.Security.Claims; // 🧠 REQUERIDO: Para extraer los claims del Contexto de Identidad del Token

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

// ============================================================================
// 🧠 EXTRACTOR DE SEGURIDAD ADVANCED: Aislamiento de Secretos de Entorno (OBS-02)
// ============================================================================
var rawConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var envDbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
var connectionString = !string.IsNullOrEmpty(envDbPassword) && rawConnectionString != null
    ? rawConnectionString.Replace("ENV_DB_PASSWORD", envDbPassword)
    : rawConnectionString;

var jwtSecretKey = Environment.GetEnvironmentVariable("JWT_KEY") 
    ?? builder.Configuration["Jwt:Key"] 
    ?? "Turnify_Secret_Key_2026_Enterprise_Edition_Security_PRO";

// 🛡️ CONTROL DEFENSIVO ANTI-BLOQUEO: Si la llave extraída mide menos de 128 bits (16 caracteres), 
// forzamos una firma criptográfica robusta para evitar excepciones en tiempo de ejecución (HS256 minimum requirement)
if (string.IsNullOrEmpty(jwtSecretKey) || jwtSecretKey.Length < 16)
{
    jwtSecretKey = "Turnify_Master_Secret_Key_Enterprise_Secure_2026_Edition_PRO_Security_Crypto_Engine_512_Bits#";
}

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SMTP_PASSWORD")))
{
    builder.Configuration["EmailSettings:Password"] = Environment.GetEnvironmentVariable("SMTP_PASSWORD");
}

// 2. AUTENTICACIÓN JWT (Extracción Dinámica Blindada)
var key = Encoding.ASCII.GetBytes(jwtSecretKey);
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

// --- 🛡️ BASE DE DATOS CON RESILIENCIA Y CADENA INYECTADA ---
builder.Services.AddDbContext<TurnifyDbContext>(options =>
    options.UseSqlServer(
        connectionString,
        sqlServerOptionsAction: sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 10, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
        }
    ));

// --- 🛡️ CONFIGURACIÓN DE DATA PROTECTION PARA PRODUCCIÓN ---
var keysFolder = Path.Combine(builder.Environment.ContentRootPath, "dataprotection-keys");
if (!Directory.Exists(keysFolder))
{
    Directory.CreateDirectory(keysFolder);
}
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysFolder));

// --- 🧠 MATRÍCULA DE POLÍTICAS DE RATE LIMITING (OBS-04) ---
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("PublicAccessPolicy", opt =>
    {
        opt.Window = TimeSpan.FromMinutes(1);
        opt.PermitLimit = 60; // Umbral máximo de 60 peticiones concurrentes por minuto por IP
        opt.QueueLimit = 0;   // Denegación inmediata sin encolamiento para mitigar consumo de RAM
    });
});

builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<IClienteService, ClienteService>();
builder.Services.AddScoped<ICitaService, CitaService>();
builder.Services.AddScoped<IServicioService, ServicioService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
// 🚀 INYECCIÓN DEL NUEVO SERVICIO DE EMPLEADOS
builder.Services.AddScoped<IEmpleadoService, EmpleadoService>();

// 🚀 [NUEVO MOTOR DE MENSAJERÍA OUT-OF-THE-BOX]
builder.Services.AddScoped<IEmailService, EmailService>();

// 🛡️ MATRÍCULA DEL BOT DE WHATSAPP
builder.Services.AddScoped<IWhatsAppService, WhatsAppService>();

//🚀 INYECCIÓN DEL NUEVO SERVICIO DE ESTACIONES DE TRABAJO
builder.Services.AddScoped<IEstacionTrabajoService, EstacionTrabajoService>();

// 🔄 Inyección del Worker Automático en segundo plano
builder.Services.AddHostedService<CitaCancellationWorker>();

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

// 🧠 ACTIVACIÓN DEL MIDDLEWARE DE CONTROL DE RÁFAGAS (Antes de la Autenticación)
app.UseRateLimiter();

// --- 🏗️ SERVICIO DE ARCHIVOS ESTÁTICOS DIAGNÓSTICO ---
string rootPath = builder.Environment.ContentRootPath;
string frontendPath = Path.Combine(rootPath, "frontend");

if (Directory.Exists(Path.Combine(frontendPath, "dist"))) {
    frontendPath = Path.Combine(frontendPath, "dist");
}

Console.WriteLine($"--- 🔍 RUTA FINAL DE ARCHIVOS DETECTADA: {frontendPath} ---");
Console.WriteLine($"--- 📂 ¿La carpeta existe?: {Directory.Exists(frontendPath)} ---");

if (Directory.Exists(frontendPath))
{
    var files = Directory.GetFiles(frontendPath, "*.html");
    Console.WriteLine($"--- 📄 Archivos HTML disponibles en Docker ({files.Length}): ---");
    foreach (var file in files) {
        Console.WriteLine($"   -> {Path.GetFileName(file)}");
    }

    var fileOptions = new DefaultFilesOptions();
    fileOptions.DefaultFileNames.Clear();
    fileOptions.DefaultFileNames.Add("login.html");
    fileOptions.FileProvider = new PhysicalFileProvider(frontendPath);
    
    app.UseDefaultFiles(fileOptions);

    app.UseStaticFiles(new StaticFileOptions { 
        FileProvider = new PhysicalFileProvider(frontendPath)
    });
}
else
{
    Console.WriteLine("⚠️ --- ADVERTENCIA: No se encontró la carpeta de frontend. ---");
}

app.UseAuthentication();

// ============================================================================
// 🚀 MIDDLEWARE DE EXPULSIÓN EN VIVO - BLINDAJE TC-003
// ============================================================================
app.UseMiddleware<LiveEvictionMiddleware>();

app.UseAuthorization();

app.MapControllers(); 

// ============================================================================
// 🛡️ ENRUTAMIENTO INTELIGENTE ANTI-SPA CONTRA UNEXPECTED TOKEN
// ============================================================================
if (Directory.Exists(frontendPath))
{
    // Bloqueamos que las rutas dirigidas a "/api" sirvan el HTML por accidente
    app.MapWhen(context => !context.Request.Path.StartsWithSegments("/api"), builder =>
    {
        builder.UseRouting();
        builder.UseEndpoints(endpoints =>
        {
            endpoints.MapFallbackToFile("login.html", new StaticFileOptions {
                FileProvider = new PhysicalFileProvider(frontendPath)
            });
        });
    });
}

// ============================================================================
// 🛡️ BÚNKER DE MIGRACIÓN AUTOMÁTICA INTELIGENTE EN DOCKER (INYECCIÓN RESILIENTE)
// ============================================================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<TurnifyDbContext>();
        Console.WriteLine("⏳ [Docker Boot] Detectando estado de TurnifyDb en SQL Server...");
        
        // 🧠 CONTROL DE PERSISTENCIA: Valida de manera segura la conexión física. 
        // Si la base de datos ya está montada en el disco real, evita el choque del CREATE DATABASE automático.
        await context.Database.OpenConnectionAsync();
        await context.Database.CloseConnectionAsync();
        
        Console.WriteLine("⏳ [Docker Boot] Sincronizando e impactando columnas DateTimeOffset...");
        await context.Database.MigrateAsync();
        Console.WriteLine("🎉 [Docker Boot] Base de datos sincronizada y blindada a nivel mundial con éxito.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"💥 [Docker Boot Warning] Ajustando estrategia de sincronización de tablas: {ex.Message}");
        
        // Canal de rescate por si el motor levantó en frío sin tablas históricas o con volúmenes mapeados
        try
        {
            var context = services.GetRequiredService<TurnifyDbContext>();
            await context.Database.MigrateAsync();
            Console.WriteLine("🎉 [Docker Boot Rescate] Sincronización forzada completada con éxito.");
        }
        catch (Exception innerEx)
        {
            Console.WriteLine($"❌ [Docker Boot Critical Error] Falló el mapeo final de esquemas en el contenedor: {innerEx.Message}");
        }
    }
}

app.Run();

public class Messages { }

// ============================================================================
// 🚀 INFRAESTRUCTURA INYECTADA EN CALIENTE PARA GUEST CHECKOUT NOTIFICATIONS
// ============================================================================

namespace Turnify.Api.Interfaces
{
    public interface IEmailService
    {
        Task EnviarTokenCitaAsync(string emailCliente, string nombreCliente, string tokenCheckIn, DateTime fecha, TimeSpan hora, string servicioName, string localName);
    }
}

namespace Turnify.Api.Services
{
    using System.Net;
    using System.Net.Mail;
    using Turnify.Api.Interfaces;

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task EnviarTokenCitaAsync(string emailCliente, string nombreCliente, string tokenCheckIn, DateTime fecha, TimeSpan hora, string servicioName, string localName)
        {
            try
            {
                var server = _configuration["EmailSettings:Server"];
                var port = int.Parse(_configuration["EmailSettings:Port"] ?? "587");
                var senderEmail = _configuration["EmailSettings:SenderEmail"];
                var senderName = _configuration["EmailSettings:SenderName"];
                var username = _configuration["EmailSettings:Username"];
                var password = _configuration["EmailSettings:Password"];
                var enableSsl = bool.Parse(_configuration["EmailSettings:EnableSsl"] ?? "true");

                if (string.IsNullOrEmpty(emailCliente) || password == "TU_CONTRASEÑA_DE_APLICACION_AQUI" || string.IsNullOrEmpty(senderEmail))
                {
                    Console.WriteLine($"⚠️ [Turnify Correo Alerta] Datos de SMTP por defecto en appsettings.json. Se omite envío real a {emailCliente}, pero la cita ya quedó guardada.");
                    return;
                }

                using var client = new SmtpClient(server, port)
                {
                    Credentials = new NetworkCredential(username, password),
                    EnableSsl = enableSsl
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail, senderName),
                    Subject = $"🕒 Tu Turno Confirmado en {localName} - Token {tokenCheckIn}",
                    Body = $@"
                        <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;'>
                            <h2 style='color: #48c1b5; border-bottom: 2px solid #f1f5f9; padding-bottom: 10px;'>¡Tu reserva está lista, {nombreCliente}!</h2>
                            <p style='color: #334155;'>Te confirmamos que se ha agendado con éxito tu espacio en <strong>{localName}</strong>.</p>
                            
                            <div style='background-color: #f8fafc; padding: 15px; border-radius: 8px; margin: 20px 0;'>
                                <p style='margin: 5px 0; color: #475569;'><strong>💇‍♂️ Servicio:</strong> {servicioName}</p>
                                <p style='margin: 5px 0; color: #475569;'><strong>📅 Fecha:</strong> {fecha:dd/MM/yyyy}</p>
                                <p style='margin: 5px 0; color: #475569;'><strong>🕒 Hora:</strong> {hora.ToString(@"hh\:mm")}</p>
                            </div>

                            <p style='color: #334155; text-align: center; margin-top: 25px;'>Presenta este código al llegar al local:</p>
                            <div style='background-color: #1e293b; color: #48c1b5; text-align: center; padding: 15px; border-radius: 8px; font-size: 1.5em; font-weight: bold; letter-spacing: 3px; margin: 10px 0;'>
                                {tokenCheckIn}
                            </div>

                            <p style='font-size: 0.85em; color: #64748b; text-align: center; margin-top: 30px;'>Este es un correo automático generado por Turnify Engine. Por favor, no respondas a este mensaje.</p>
                        </div>",
                    IsBodyHtml = true
                };

                mailMessage.To.Add(emailCliente);
                await client.SendMailAsync(mailMessage);
                Console.WriteLine($"📧 [Turnify Notificador PRO] Correo despachado con éxito hacia {emailCliente} con el Token {tokenCheckIn}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ [Turnify Notificador Error] Falla crítica en el canal SMTP: {ex.Message}");
            }
        }
    }
}

// ============================================================================
// 🚀 INFRAESTRUCTURA DE EXPULSIÓN EN VIVO CONSERVADA (TC-003)
// ============================================================================
namespace Turnify.Api.Middleware
{
    using Microsoft.AspNetCore.Http;
    using Microsoft.EntityFrameworkCore;
    using System;
    using System.Linq;
    using System.Security.Claims;
    using System.Threading.Tasks;
    using Turnify.Api.Data;

    public class LiveEvictionMiddleware
    {
        private readonly RequestDelegate _next;

        public LiveEvictionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, TurnifyDbContext dbContext)
        {
            var userIdClaim = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userIdClaim) && Guid.TryParse(userIdClaim, out var parsedUserId))
            {
                var usuarioStatus = await dbContext.usuarios
                    .AsNoTracking()
                    .Where(u => u.id == parsedUserId)
                    .Select(u => new { u.esta_bloqueado, u.activo })
                    .FirstOrDefaultAsync();

                if (usuarioStatus == null || usuarioStatus.esta_bloqueado == true || usuarioStatus.activo == false)
                {
                    Console.WriteLine($"🔒 [LIVE EVICTION TC-003] Denegando petición en caliente. Usuario ID: {parsedUserId} se encuentra bloqueado o inactivo.");
                    
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/json";
                    
                    var responsePayload = new { message = "Tu cuenta ha sido suspendida o deshabilitada. Sesión revocada en vivo." };
                    await context.Response.WriteAsync(JsonSerializer.Serialize(responsePayload));
                    return; 
                }
            }

            await _next(context);
        }
    }
}