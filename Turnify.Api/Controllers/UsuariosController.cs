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
using System.Linq;
using Microsoft.Extensions.Logging; 

namespace Turnify.Api.Controllers
{
    [Route("api/Usuarios")] 
    [ApiController]
    public class UsuariosController : ControllerBase
    {
        private readonly IUsuarioService _usuarioService;
        private readonly IConfiguration _config;
        private readonly TurnifyDbContext _context;
        private readonly ILogger<UsuariosController> _logger; 

        public UsuariosController(
            IUsuarioService usuarioService, 
            IConfiguration config, 
            TurnifyDbContext context,
            ILogger<UsuariosController> logger)
        {
            _usuarioService = usuarioService;
            _config = config;
            _context = context;
            _logger = logger;
        }

        // 1. OBTENER TODOS LOS USUARIOS (Multi-tenant)
        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAll()
        {
            _logger.LogInformation("--- 🔍 GET: Listando todos los usuarios ---");

            var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(usuarioIdClaim)) return Unauthorized(new { message = "Sesión no válida" });

            var userId = Guid.Parse(usuarioIdClaim);
            var rolClaim = User.FindFirst(ClaimTypes.Role)?.Value;
            
            var isAdmin = rolClaim != null && (rolClaim.ToLower().Contains("admin") || rolClaim.ToLower().Contains("super"));

            IQueryable<Usuarios> query = _context.usuarios.Include(u => u.Rol);

            if (!isAdmin)
            {
                var proveedor = await _context.proveedores
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UsuarioId == userId || p.Id == userId);

                if (proveedor != null)
                {
                    var clienteIds = await _context.citas
                        .AsNoTracking()
                        .Where(c => c.ProveedorId == proveedor.Id)
                        .Select(c => c.ClienteId)
                        .Distinct()
                        .ToListAsync();

                    // 🛡️ AJUSTE KILLER: Eliminamos 'u.id == userId' para que el barbero 
                    // no se vea a sí mismo en la gestión de sus clientes.
                    query = query.Where(u => clienteIds.Contains(u.id));
                }
                else
                {
                    query = query.Where(u => u.id == userId);
                }
            }

            var usuarios = await query
                .Select(u => new {
                    u.id,
                    u.nombre,
                    u.email,
                    rol = u.Rol != null ? u.Rol.nombre : "Sin Rol", 
                    u.esta_bloqueado,
                    u.suscripcion_fin,
                    u.rol_id
                })
                .ToListAsync();

            return Ok(usuarios);
        }

        // 2. LOGIN - 🚩 CORREGIDO: Namespace explícito para evitar error CS1061 y CS1503
        [HttpPost("login")]
        [AllowAnonymous] 
        public async Task<IActionResult> Login([FromBody] Turnify.Api.Models.DTOs.LoginDto dto)
        {
            _logger.LogInformation("--- 📩 Intento de Login: {Email} ---", dto?.Email ?? "NULO");

            if (dto == null) return BadRequest(new { message = "Cuerpo de petición nulo." });

            try 
            {
                var result = await _usuarioService.LoginAsync(dto);
                
                if (!result.Success) 
                {
                    _logger.LogWarning("--- ⚠️ Fallo de Auth: {Message} ---", result.Message);
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

                    _logger.LogInformation("--- ✅ Login Exitoso: {Email} ---", usuarioConRol.email);

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
                _logger.LogError(ex, "--- 🚨 CRASH EN LOGIN ---");
                return StatusCode(500, new { message = ex.Message }); 
            }
        }

        // 3. REGISTRAR
        [HttpPost("registrar")]
        [AllowAnonymous]
        public async Task<IActionResult> Registrar([FromBody] Turnify.Api.Models.DTOs.UsuarioRegistroDTO dto)
        {
            _logger.LogInformation("--- 📝 Intento de registro para: {Email} ---", dto?.Email);
            
            if (dto == null) return BadRequest(new { message = "Datos inválidos." });

            try 
            {
                var result = await _usuarioService.RegistrarAsync(dto);
                
                if (result.Success)
                    return Ok(new { message = "¡Registro exitoso!", usuarioId = result.UsuarioId });
                
                return BadRequest(new { message = result.Message });
            }
            catch (Exception ex) 
            { 
                _logger.LogError(ex, "--- 🚨 ERROR EN REGISTRO ---");
                return StatusCode(500, new { message = "Error interno del servidor." }); 
            }
        }

        // 4. RECUPERACIÓN DE CONTRASEÑA
        [HttpPost("forgot-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ForgotPassword([FromBody] Turnify.Api.Models.DTOs.ForgotPasswordDto dto)
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
        public async Task<IActionResult> ResetPassword([FromBody] Turnify.Api.Models.DTOs.ResetPasswordDto dto)
        {
            var emailInput = dto.Email?.Trim().ToLower() ?? string.Empty;
            var telefonoInput = new string((dto.Telefono ?? "").Where(char.IsDigit).ToArray()); 
            var tokenInput = dto.Token?.Trim() ?? string.Empty;

            _logger.LogInformation("--- 🔑 Intento de Reset para: {Email} ---", emailInput);

            // 🛡️ Blindaje 1: Validación por Token primero
            var usuario = await _context.usuarios.FirstOrDefaultAsync(u => 
                u.ResetToken == tokenInput && !string.IsNullOrEmpty(tokenInput) && u.ResetTokenExpires > DateTime.UtcNow);

            if (usuario == null)
            {
                _logger.LogWarning("Token inválido para {Email}. Iniciando Validación Dual...", emailInput);
                
                usuario = await _context.usuarios.FirstOrDefaultAsync(u => u.email != null && u.email.ToLower() == emailInput);
                
                if (usuario != null)
                {
                    // 🛡️ Blindaje 2: Validación Dual usando la nueva simetría de tablas
                    var esClienteValido = await _context.clientes
                        .AnyAsync(c => c.usuario_id == usuario.id && 
                                       c.email != null && c.email.ToLower() == emailInput &&
                                       c.telefono != null && 
                                       EF.Functions.Like(c.telefono, $"%{telefonoInput}%"));
                    
                    var matchCliente = esClienteValido;

                    bool matchProveedor = false;
                    try 
                    {
                        // 🚩 AJUSTE KILLER: Ahora usamos la columna p.Email que creamos
                        matchProveedor = await _context.proveedores
                            .AnyAsync(p => p.UsuarioId == usuario.id && 
                                           p.Email != null && p.Email.ToLower() == emailInput &&
                                           p.Telefono != null && 
                                           EF.Functions.Like(p.Telefono, $"%{telefonoInput}%"));
                    }
                    catch (Exception ex) 
                    { 
                        _logger.LogError("Error en validación dual proveedor: {Msg}", ex.Message); 
                    }

                    if (!matchCliente && !matchProveedor)
                    {
                        _logger.LogWarning("❌ Validación Dual Fallida: Teléfono o Email no coinciden para {Email}", emailInput);
                        usuario = null; 
                    }
                }
            }

            if (usuario == null) 
            {
                return BadRequest(new { message = "Los datos de validación son incorrectos o el enlace ha expirado." });
            }

            try 
            {
                usuario.password_hash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
                usuario.ResetToken = null;
                usuario.ResetTokenExpires = null;

                await _context.SaveChangesAsync();
                _logger.LogInformation("✅ Éxito: Password actualizado para {Email}", usuario.email);
                return Ok(new { message = "Contraseña actualizada correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "--- 🚨 ERROR CRÍTICO AL GUARDAR PASS ---");
                return StatusCode(500, new { message = "Error al procesar el cambio de contraseña." });
            }
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
            catch (Exception ex) 
            { 
                _logger.LogError(ex, "Error al obtener estadísticas");
                return StatusCode(500, new { message = "Error al obtener estadísticas" }); 
            }
        }

        [HttpPut("renovar/{id:guid}")]
        [Authorize]
        public async Task<IActionResult> RenovarSuscripcion(Guid id, [FromQuery] int meses = 1)
        {
            var usuario = await _context.usuarios.FindAsync(id);
            if (usuario == null) return NotFound();

            var fechaBase = (usuario.suscripcion_fin.HasValue && usuario.suscripcion_fin.Value > DateTime.UtcNow) 
                                ? usuario.suscripcion_fin.Value 
                                : DateTime.UtcNow;

            usuario.suscripcion_fin = fechaBase.AddMonths(meses);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Suscripción extendida", nuevaFecha = usuario.suscripcion_fin });
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
}