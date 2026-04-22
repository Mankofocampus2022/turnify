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
    [Route("api/Usuarios")] 
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

        // 1. OBTENER TODOS LOS USUARIOS
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAll()
        {
            Console.WriteLine("--- 🔍 GET: Listando todos los usuarios ---");
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

        // 2. LOGIN (Blindado contra colisiones de nombres)
        [HttpPost("login")]
        [AllowAnonymous] 
        public async Task<IActionResult> Login([FromBody] Turnify.Api.Models.DTOs.LoginDto dto)
        {
            Console.WriteLine($"--- 📩 Intento de Login: {dto?.Email ?? "EMAIL NULO"} ---");

            if (dto == null) return BadRequest(new { message = "Cuerpo de petición nulo." });

            try 
            {
                // Usamos el DTO de la capa de Modelos explícitamente
                var result = await _usuarioService.LoginAsync(dto);
                
                if (!result.Success) 
                {
                    Console.WriteLine($"--- ⚠️ Fallo de Auth: {result.Message} ---");
                    return Unauthorized(new { message = result.Message });
                }

                if (result.Data is Usuarios usuarioLogueado)
                {
                    var usuarioConRol = await _context.usuarios
                        .Include(u => u.Rol)
                        .FirstOrDefaultAsync(u => u.id == usuarioLogueado.id);

                    if (usuarioConRol == null) return Unauthorized(new { message = "Error al recuperar perfil." });

                    var proveedor = await _context.proveedores.FirstOrDefaultAsync(p => p.UsuarioId == usuarioConRol.id);
                    var token = GenerarTokenJWT(usuarioConRol);

                    Console.WriteLine($"--- ✅ Login Exitoso: {usuarioConRol.email} ---");

                    return Ok(new { 
                        token = token, 
                        user = new { 
                            id = usuarioConRol.id, 
                            nombre = usuarioConRol.nombre, 
                            email = usuarioConRol.email, 
                            rol = usuarioConRol.Rol?.nombre ?? "Usuario",
                            proveedorId = proveedor?.Id 
                        } 
                    });
                }
                return StatusCode(500, new { message = "Error de formato en datos." });
            }
            catch (Exception ex) 
            { 
                Console.WriteLine($"--- 🚨 CRASH EN LOGIN: {ex.Message} ---");
                return StatusCode(500, new { message = ex.Message }); 
            }
        }

        // 3. REGISTRAR (Sincronizado con el nuevo UsuarioService)
        [HttpPost("registrar")]
        [AllowAnonymous]
        public async Task<IActionResult> Registrar([FromBody] UsuarioRegistroDTO dto)
        {
            Console.WriteLine($"--- 📝 Intento de registro para: {dto?.Email} ---");
            
            if (dto == null) return BadRequest(new { message = "Datos inválidos." });

            try 
            {
                // Pasamos el DTO directamente al servicio (él maneja la transacción)
                var result = await _usuarioService.RegistrarAsync(dto);
                
                if (result.Success)
                    return Ok(new { message = "¡Registro exitoso!", usuarioId = result.UsuarioId });
                
                return BadRequest(new { message = result.Message });
            }
            catch (Exception ex) 
            { 
                Console.WriteLine($"--- 🚨 ERROR EN REGISTRO: {ex.Message} ---");
                return StatusCode(500, new { message = "Error interno del servidor." }); 
            }
        }

        // 4. RECUPERACIÓN DE CONTRASEÑA
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var usuario = await _context.usuarios.FirstOrDefaultAsync(u => u.email == dto.Email);
            if (usuario == null) return BadRequest(new { message = "El correo no existe." });

            usuario.ResetToken = Guid.NewGuid().ToString();
            usuario.ResetTokenExpires = DateTime.UtcNow.AddHours(1); 

            await _context.SaveChangesAsync();
            return Ok(new { message = "Token generado", token = usuario.ResetToken });
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
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

        // 5. GESTIÓN Y ESTADÍSTICAS
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
        public async Task<IActionResult> RenovarSuscripcion(Guid id, [FromQuery] int meses = 1)
        {
            var usuario = await _context.usuarios.FindAsync(id);
            if (usuario == null) return NotFound();

            DateTime fechaBase = (usuario.suscripcion_fin.HasValue && usuario.suscripcion_fin.Value > DateTime.UtcNow) 
                                ? usuario.suscripcion_fin.Value 
                                : DateTime.UtcNow;

            usuario.suscripcion_fin = fechaBase.AddMonths(meses);
            await _context.SaveChangesAsync();
            return Ok(new { message = $"Suscripción extendida", nuevaFecha = usuario.suscripcion_fin });
        }

        // 6. CRUD BÁSICO
        [HttpPut("{id:guid}")] 
        public async Task<IActionResult> Update(Guid id, [FromBody] Usuarios u) 
        { 
            if (id != u.id) return BadRequest(); 
            return await _usuarioService.ActualizarAsync(u) ? Ok() : BadRequest(); 
        }

        [HttpDelete("{id:guid}")] 
        public async Task<IActionResult> Delete(Guid id) 
        { 
            return await _usuarioService.EliminarLogicoAsync(id) ? Ok() : NotFound(); 
        }

        [HttpGet("{id:guid}")] 
        public async Task<IActionResult> GetById(Guid id) 
        { 
            var u = await _usuarioService.GetUsuarioByIdAsync(id); 
            return u == null ? NotFound() : Ok(u); 
        }

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

    // 🚩 DTOs auxiliares (Ubicados aquí para evitar errores si no están en la carpeta DTOs)
    // NOTA: No incluimos LoginDto aquí para evitar la colisión que causó el error de Docker.
    public class ForgotPasswordDto { public string Email { get; set; } }
    public class ResetPasswordDto { public string Token { get; set; } public string NewPassword { get; set; } }
}