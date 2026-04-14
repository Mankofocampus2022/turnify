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

        // --- MÉTODOS DE LECTURA ---

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAll()
        {
            var usuarios = await _context.usuarios
                .AsNoTracking()
                .Include(u => u.Rol)
                .Select(u => new {
                    u.id,
                    u.nombre,
                    u.email,
                    rol = u.Rol != null ? u.Rol.nombre : "Sin Rol", 
                    esta_bloqueado = u.esta_bloqueado, 
                    u.suscripcion_fin,
                    u.rol_id
                })
                .ToListAsync();

            return Ok(usuarios);
        }

        [HttpGet("{id:guid}")] 
        public async Task<IActionResult> GetById(Guid id) 
        { 
            var u = await _usuarioService.GetUsuarioByIdAsync(id); 
            return u == null ? NotFound(new { message = "Usuario no encontrado" }) : Ok(u); 
        }

        // --- AUTENTICACIÓN Y REGISTRO ---

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] Turnify.Api.Models.DTOs.LoginDto dto)
        {
            try 
            {
                var result = await _usuarioService.LoginAsync(dto);
                if (!result.Success) return Unauthorized(new { message = result.Message });

                if (result.Data is Usuarios u) 
                {
                    var proveedor = await _context.proveedores.FirstOrDefaultAsync(p => p.UsuarioId == u.id);
                    var token = GenerarTokenJWT(u);
                    return Ok(new { 
                        token, 
                        user = new { 
                            id = u.id, 
                            u.nombre, 
                            u.email, 
                            rol = u.Rol?.nombre, 
                            proveedorId = proveedor?.Id 
                        } 
                    });
                }
                return StatusCode(500, "Error de formato de datos.");
            } 
            catch (Exception ex) { return StatusCode(500, ex.Message); }
        }

        [HttpPost("registrar")]
        public async Task<IActionResult> Registrar([FromBody] UsuarioRegistroDTO dto)
        {
            if (dto == null) return BadRequest(new { message = "Los datos de registro son obligatorios." });

            try 
            {
                var result = await _usuarioService.RegistrarAsync(dto);
                return result.Success 
                    ? Ok(new { message = result.Message, usuarioId = result.UsuarioId }) 
                    : BadRequest(new { message = result.Message });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Error interno: " + ex.Message }); }
        }

        // --- GESTIÓN DE CONTRASEÑAS ---

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var usuario = await _context.usuarios.FirstOrDefaultAsync(u => u.email == dto.Email);
            if (usuario == null) return BadRequest(new { message = "El correo no existe." });

            usuario.ResetToken = Guid.NewGuid().ToString();
            usuario.ResetTokenExpires = DateTime.UtcNow.AddHours(1); 

            await _context.SaveChangesAsync();
            return Ok(new { message = "Token generado con éxito.", token = usuario.ResetToken });
        }

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
            return Ok(new { message = "Contraseña actualizada correctamente." });
        }

        // --- GESTIÓN DE SUSCRIPCIÓN Y ESTADO ---

        // 🚩 VERSIÓN UNIFICADA (Sin duplicados y con manejo de nulos)
        [HttpPut("renovar/{id:guid}")]
        [Authorize]
        public async Task<IActionResult> RenovarSuscripcion(Guid id, [FromQuery] int meses = 1)
        {
            try 
            {
                var usuario = await _context.usuarios.FindAsync(id);
                if (usuario == null) return NotFound(new { message = "Usuario no encontrado." });

                // Lógica Senior: Si la suscripción aún no vence, sumamos desde el vencimiento.
                // Si ya venció (o es nula), sumamos desde hoy (DateTime.UtcNow).
                DateTime fechaFinActual = usuario.suscripcion_fin ?? DateTime.UtcNow;
                DateTime fechaBase = (fechaFinActual > DateTime.UtcNow) ? fechaFinActual : DateTime.UtcNow;

                usuario.suscripcion_fin = fechaBase.AddMonths(meses);
                
                await _context.SaveChangesAsync();
                
                return Ok(new { 
                    message = $"Suscripción extendida por {meses} mes(es)", 
                    nuevaFecha = usuario.suscripcion_fin 
                });
            }
            catch (Exception ex) 
            { 
                return StatusCode(500, new { message = "Error al renovar: " + ex.Message }); 
            }
        }

        [HttpPut("cambiar-estado/{id:guid}")]
        [Authorize]
        public async Task<IActionResult> CambiarEstado(Guid id, [FromQuery] bool bloquear)
        {
            var exito = await _usuarioService.CambiarEstadoBloqueoAsync(id, bloquear);
            return exito ? Ok(new { message = "Estado actualizado correctamente" }) : NotFound();
        }

        // --- ESTADÍSTICAS ---

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

        // --- CRUD BÁSICO ---

        [HttpPut("{id:guid}")] 
        public async Task<IActionResult> Update(Guid id, [FromBody] Usuarios u) 
        { 
            if (id != u.id) return BadRequest(new { message = "El ID no coincide." }); 
            return await _usuarioService.ActualizarAsync(u) ? Ok(new { message = "Actualizado" }) : BadRequest(); 
        }

        [HttpDelete("{id:guid}")] 
        public async Task<IActionResult> Delete(Guid id) 
        { 
            return await _usuarioService.EliminarLogicoAsync(id) ? Ok(new { message = "Eliminado" }) : NotFound(); 
        }

        // --- SEGURIDAD ---

        private string GenerarTokenJWT(Usuarios usuario)
        {
            var jwtKey = _config["Jwt:Key"] ?? "Clave_Super_Secreta_2026_Turnify_Darwin";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            
            var claims = new[] { 
                new Claim(ClaimTypes.NameIdentifier, usuario.id.ToString()), 
                new Claim(ClaimTypes.Name, usuario.nombre ?? ""), 
                new Claim(ClaimTypes.Role, usuario.Rol?.nombre ?? "Usuario") 
            };

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var tokenDescriptor = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"] ?? "Turnify.Api",
                audience: _config["Jwt:Audience"] ?? "Turnify.App",
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(1440),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
        }
    }
}