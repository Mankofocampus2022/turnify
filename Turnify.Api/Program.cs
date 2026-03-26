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
// --- 🚀 CONFIGURACIÓN DE CONTROLADORES CON FLEXIBILIDAD JSON ---
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Esto permite que 'rol_id', 'Rol_id' o 'RolId' mapeen correctamente
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        // Mantiene la política de nombres en CamelCase pero respetando los atributos del modelo
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

builder.Services.AddDbContext<TurnifyDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IUsuarioService, UsuarioService>();
builder.Services.AddScoped<IClienteService, ClienteService>();
builder.Services.AddScoped<ICitaService, CitaService>();
builder.Services.AddScoped<IServicioService, ServicioService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();

var app = builder.Build();

// 3. MIDDLEWARES (EL ORDEN IMPORTA)
app.UseRequestLocalization(localizationOptions); 
app.UseMiddleware<ExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c => {
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Turnify API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowTurnify"); 
app.UseAuthentication(); 
app.UseAuthorization();

app.MapControllers();
app.MapGet("/", () => "Turnify API corriendo OK con Seguridad JWT y Multidioma 🌎");

// 4. VERIFICACIÓN DE BASE DE DATOS
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<TurnifyDbContext>();
        if (context.Database.CanConnect()) 
            Console.WriteLine("--- ✅ la conexion es un exito mi perro ---");
        else 
        {
            context.Database.EnsureCreated();
            Console.WriteLine("--- 📦 DB creada ---");
        }
    }
    catch (Exception ex) { Console.WriteLine($"--- ❌ Error DB: {ex.Message} ---"); }
}

app.Run();

public class Messages { }