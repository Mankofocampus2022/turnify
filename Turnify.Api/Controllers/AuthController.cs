using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Turnify.Api.Data;
using Turnify.Api.Models;
// Quitamos el DTO de afuera para evitar el choque de nombres
// using Turnify.Api.Models.DTOs; 

namespace Turnify.Api.Controllers
{
    // 🚩 Definimos el DTO aquí arriba para que sea el que mande
    public class InternalLoginDto 
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _config;
        private readonly TurnifyDbContext _context;

        public AuthController(IConfiguration config, TurnifyDbContext context)
        {
            _config = config;
            _context = context;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] InternalLoginDto login)
        {
            // 1. Buscamos el usuario (email en minúsculas)
            var usuario = await _context.usuarios
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.email == login.Email);

            // 2. Validación de credenciales
            if (usuario == null || usuario.password_hash != login.Password) 
            {
                // Soporte para tu cuenta admin
                if (login.Email == "admin" && login.Password == "Turnify2026!")
                {
                    var tokenAdmin = GenerarToken(Guid.NewGuid().ToString(), "admin", "Admin", null, null);
                    return Ok(new { token = tokenAdmin, user = new { nombre = "Admin", rol = "Admin" } });
                }
                
                return Unauthorized(new { message = "Credenciales incorrectas" });
            }

            // 3. 🚩 AUTO-VINCULACIÓN (Solución al Cliente NULL de Alexandra)
            var cliente = await _context.clientes
                .FirstOrDefaultAsync(c => c.usuario_id == usuario.id);

            if (cliente == null && usuario.Rol?.nombre == "Cliente")
            {
                cliente = new Clientes
                {
                    id = Guid.NewGuid(),
                    usuario_id = usuario.id,
                    nombre = usuario.nombre,
                    email = usuario.email,
                    telefono = "3110000000",
                    activo = true,
                    fecha_creacion = DateTime.UtcNow
                };
                _context.clientes.Add(cliente);
                await _context.SaveChangesAsync();
            }

            // 4. Buscamos Proveedor
            var proveedorId = await _context.proveedores
                .Where(p => p.UsuarioId == usuario.id) 
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync();

            var rolNombre = usuario.Rol?.nombre ?? "Cliente";
            
            // 5. Generamos el Token con el NameIdentifier que pide CitasController
            var token = GenerarToken(usuario.id.ToString(), usuario.email, rolNombre, cliente?.id, proveedorId);

            return Ok(new { 
                token = token,
                user = new {
                    id = usuario.id,
                    clienteId = cliente?.id, 
                    proveedorId = proveedorId,
                    nombre = usuario.nombre,
                    rol = rolNombre,
                    email = usuario.email
                }
            });
        }

        private string GenerarToken(string userId, string usuarioEmail, string rol, Guid? clienteId, Guid? proveedorId)
        {
            var key = Encoding.ASCII.GetBytes(_config["Jwt:Key"] ?? "Llave_Super_Secreta_De_Respaldo_32_Chars");
            
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId), 
                new Claim(ClaimTypes.Name, usuarioEmail ?? "Usuario"),
                new Claim(ClaimTypes.Role, rol ?? "Cliente")
            };

            if (clienteId.HasValue) claims.Add(new Claim("ClienteId", clienteId.Value.ToString()));
            if (proveedorId.HasValue) claims.Add(new Claim("ProveedorId", proveedorId.Value.ToString()));

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(24),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key), 
                    SecurityAlgorithms.HmacSha256Signature),
                Issuer = _config["Jwt:Issuer"],
                Audience = _config["Jwt:Audience"]
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            
            return tokenHandler.WriteToken(token);
        }
    }
}