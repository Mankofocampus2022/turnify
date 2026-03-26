using Microsoft.AspNetCore.Mvc;
using Turnify.Api.Models;
using Turnify.Api.Models.DTOs;
using Turnify.Api.Interfaces;
using Turnify.Api.Data;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Turnify.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsuariosController : ControllerBase
    {
        private readonly IUsuarioService _usuarioService;
        private readonly IConfiguration _config;
        private readonly TurnifyDbContext _context;

        public UsuariosController(IUsuarioService usuarioService, IConfiguration config, TurnifyDbContext context)
        {
            _usuarioService = usuarioService;
            _config = config;
            _context = context;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAll()
        {
            var usuarios = await _context.usuarios
                .Include(u => u.Rol)
                .Select(u => new {
                    id = u.id,
                    nombre = u.nombre,
                    email = u.email,
                    rol = u.Rol != null ? u.Rol.nombre : "Sin Rol", 
                    esta_bloqueado = u.esta_bloqueado,
                    suscripcion_fin = u.suscripcion_fin,
                    rol_id = u.rol_id
                })
                .ToListAsync();

            return Ok(usuarios);
        }

        // 1. LOGIN (Ya incluye el ProveedorId para evitar errores 500 en servicios)
        // 1. LOGIN (Corregido: Ruta de DTO y Mayúscula en Id)
[HttpPost("login")]
public async Task<IActionResult> Login([FromBody] Turnify.Api.Models.DTOs.LoginDto dto)
{
    try 
    {
        var result = await _usuarioService.LoginAsync(dto);
        
        if (!result.Success) 
        {
            if (result.Message.Contains("suspendida"))
                return StatusCode(403, new { message = result.Message });

            if (result.Message.Contains("vencido") || result.Message.Contains("suscripción"))
                return StatusCode(402, new { message = result.Message });

            return Unauthorized(new { message = result.Message });
        }

        if (result.Data is Usuarios usuarioLogueado)
        {
            var usuarioConRol = await _context.usuarios
                .Include(u => u.Rol)
                .FirstOrDefaultAsync(u => u.id == usuarioLogueado.id);

            if (usuarioConRol == null) 
            {
                return Unauthorized(new { message = "Error al recuperar el perfil del usuario." });
            }

            // 🚩 BUSCAMOS EL PROVEEDOR
            var proveedor = await _context.proveedores
                .FirstOrDefaultAsync(p => p.UsuarioId == usuarioConRol.id);

            var token = GenerarTokenJWT(usuarioConRol);

            return Ok(new { 
                token = token, 
                user = new { 
                    id = usuarioConRol.id, 
                    nombre = usuarioConRol.nombre, 
                    email = usuarioConRol.email, 
                    rol = usuarioConRol.Rol?.nombre ?? "Usuario",
                    // 🚩 CORRECCIÓN: Usamos Id con Mayúscula para que coincida con el Modelo
                    proveedorId = proveedor?.Id 
                } 
            });
        }
        
        return StatusCode(500, new { message = "Error de formato en los datos del usuario." });
    }
    catch (Exception ex) 
    { 
        return StatusCode(500, new { message = ex.Message }); 
    }
}
        // 2. FORGOT PASSWORD
        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var usuario = await _context.usuarios.FirstOrDefaultAsync(u => u.email == dto.Email);
            if (usuario == null) return BadRequest(new { message = "El correo no existe." });

            usuario.ResetToken = Guid.NewGuid().ToString();
            usuario.ResetTokenExpires = DateTime.UtcNow.AddHours(1); 

            await _context.SaveChangesAsync();
            return Ok(new { message = "Token generado", token = usuario.ResetToken });
        }

        // 3. RESET PASSWORD
        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            var usuario = await _context.usuarios.FirstOrDefaultAsync(u => 
                u.ResetToken == dto.Token && u.ResetTokenExpires > DateTime.UtcNow);

            if (usuario == null) return BadRequest(new { message = "Token inválido o expirado." });

            usuario.password_hash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
            usuario.ResetToken = null;
            usuario.ResetTokenExpires = null;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Contraseña actualizada." });
        }

        // 4. REGISTRAR
        [HttpPost("registrar")]
        public async Task<IActionResult> Registrar([FromBody] UsuarioRegistroDTO dto)
        {
            try 
            {
                var nuevoUsuario = new Usuarios { 
                    nombre = dto.Nombre, 
                    email = dto.Email, 
                    password_hash = dto.Password, 
                    rol_id = dto.RolId 
                };

                var result = await _usuarioService.RegistrarAsync(nuevoUsuario);
                
                return result.Success 
                    ? Ok(new { message = "Usuario creado", usuarioId = result.UsuarioId }) 
                    : BadRequest(new { message = result.Message });
            }
            catch (Exception ex) 
            { 
                return StatusCode(500, new { message = "Error: " + ex.Message }); 
            }
        }

        [HttpPut("cambiar-estado/{id:guid}")]
        [Authorize]
        public async Task<IActionResult> CambiarEstado(Guid id, [FromQuery] bool bloquear)
        {
            var exito = await _usuarioService.CambiarEstadoBloqueoAsync(id, bloquear);
            return exito ? Ok(new { message = "Estado actualizado" }) : NotFound();
        }

        [HttpGet("dashboard-stats")]
        [Authorize] 
        public async Task<IActionResult> GetDashboardStats()
        {
            try
            {
                var usuariosCount = await _usuarioService.GetTotalUsuariosActivosAsync();
                var proveedoresCount = await _context.proveedores.CountAsync(); 
                return Ok(new { usuariosCount, proveedoresCount, ingresosMensuales = 0 });
            }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        [HttpPut("renovar/{id:guid}")]
        [Authorize]
        public async Task<IActionResult> RenovarSuscripcion(Guid id)
        {
            try 
            {
                var usuario = await _context.usuarios.FindAsync(id);
                if (usuario == null) return NotFound();

                DateTime fechaBase = (usuario.suscripcion_fin.HasValue && usuario.suscripcion_fin.Value > DateTime.UtcNow) 
                                    ? usuario.suscripcion_fin.Value 
                                    : DateTime.UtcNow;

                usuario.suscripcion_fin = fechaBase.AddDays(30);
                await _context.SaveChangesAsync();
                return Ok(new { message = "Renovado", nuevaFecha = usuario.suscripcion_fin });
            }
            catch (Exception ex) { return StatusCode(500, new { message = ex.Message }); }
        }

        [HttpPut("{id:guid}")] public async Task<IActionResult> Update(Guid id, [FromBody] Usuarios u) { if (id != u.id) return BadRequest(); return await _usuarioService.ActualizarAsync(u) ? Ok() : BadRequest(); }
        [HttpDelete("{id:guid}")] public async Task<IActionResult> Delete(Guid id) { return await _usuarioService.EliminarLogicoAsync(id) ? Ok() : NotFound(); }
        [HttpGet("{id:guid}")] public async Task<IActionResult> GetById(Guid id) { var u = await _usuarioService.GetUsuarioByIdAsync(id); return u == null ? NotFound() : Ok(u); }

        private string GenerarTokenJWT(Usuarios usuario)
        {
            var jwtKey = _config["Jwt:Key"] ?? "Clave_Super_Secreta_2026_Turnify_Darwin";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            
            var claims = new[] { 
                new Claim(ClaimTypes.NameIdentifier, usuario.id.ToString()), 
                new Claim(ClaimTypes.Name, usuario.nombre ?? ""), 
                new Claim(ClaimTypes.Role, usuario.Rol?.nombre ?? "Usuario") 
            };

            var tokenDescriptor = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"] ?? "Turnify.Api",
                audience: _config["Jwt:Audience"] ?? "Turnify.App",
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(1440),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            );
            return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
        }
    }
}