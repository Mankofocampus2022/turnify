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
                    var userIds = await _context.clientes.AsNoTracking()
                        .Where(c => _context.citas.Any(cita => cita.ProveedorId == proveedor.Id && cita.ClienteId == c.id))
                        .Select(c => c.usuario_id)
                        .Distinct()
                        .ToListAsync();

                    query = query.Where(u => userIds.Contains(u.id));
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

        // 2. LOGIN - 🚩 REPARACIÓN DE IDENTIDAD PARA CLIENTES
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

                    // 🚩 [NUEVO] AUTO-VINCULACIÓN DE CLIENTE (Alexandra Fix)
                    var cliente = await _context.clientes.FirstOrDefaultAsync(c => c.usuario_id == usuarioConRol.id);
                    
                    if (cliente == null && (usuarioConRol.Rol?.nombre ?? "").Contains("Cliente", StringComparison.OrdinalIgnoreCase))
                    {
                        cliente = new Clientes {
                            id = Guid.NewGuid(),
                            usuario_id = usuarioConRol.id,
                            nombre = usuarioConRol.nombre,
                            email = usuarioConRol.email,
                            telefono = "3110000000",
                            activo = true,
                            fecha_creacion = DateTime.UtcNow
                        };
                        _context.clientes.Add(cliente);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation("✅ [Turnify] Cliente creado automáticamente para: {Email}", usuarioConRol.email);
                    }

                    var proveedor = await _context.proveedores.FirstOrDefaultAsync(p => p.UsuarioId == usuarioConRol.id);
                    
                    // 🚩 [NUEVO] Generamos token pasando todos los IDs para que viajen en los Claims
                    var token = GenerarTokenJWT(usuarioConRol, cliente?.id, proveedor?.Id);

                    _logger.LogInformation("--- ✅ Login Exitoso: {Email} ---", usuarioConRol.email);

                    return Ok(new { 
                        token = token, 
                        user = new { 
                            id = usuarioConRol.id,
                            clienteId = cliente?.id, // 🔑 AHORA SÍ LLEGA EL ID AL FRONTEND
                            proveedorId = proveedor?.Id,
                            nombre = usuarioConRol.nombre, 
                            email = usuarioConRol.email, 
                            rol = usuarioConRol.Rol?.nombre ?? "Usuario"
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

        // 4. RECUPERACIÓN DE CONTRASEÑA... (Lógica de Forgot/Reset Intacta)
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

            var usuario = await _context.usuarios.FirstOrDefaultAsync(u => 
                u.ResetToken == tokenInput && !string.IsNullOrEmpty(tokenInput) && u.ResetTokenExpires > DateTime.UtcNow);

            if (usuario == null)
            {
                _logger.LogWarning("Token inválido para {Email}. Iniciando Validación Dual...", emailInput);
                usuario = await _context.usuarios.FirstOrDefaultAsync(u => u.email != null && u.email.ToLower() == emailInput);
                
                if (usuario != null)
                {
                    var esClienteValido = await _context.clientes
                        .AnyAsync(c => c.usuario_id == usuario.id && 
                                       c.email != null && c.email.ToLower() == emailInput &&
                                       c.telefono != null && 
                                       EF.Functions.Like(c.telefono, $"%{telefonoInput}%"));
                    
                    bool matchProveedor = false;
                    try 
                    {
                        matchProveedor = await _context.proveedores
                            .AnyAsync(p => p.UsuarioId == usuario.id && 
                                           p.Email != null && p.Email.ToLower() == emailInput &&
                                           p.Telefono != null && 
                                           EF.Functions.Like(p.Telefono, $"%{telefonoInput}%"));
                    }
                    catch (Exception ex) { _logger.LogError("Error en validación dual: {Msg}", ex.Message); }

                    if (!esClienteValido && !matchProveedor) usuario = null; 
                }
            }

            if (usuario == null) return BadRequest(new { message = "Datos de validación incorrectos." });

            try 
            {
                usuario.password_hash = BCrypt.Net.BCrypt.HashPassword(dto.NewPassword);
                usuario.ResetToken = null;
                usuario.ResetTokenExpires = null;
                await _context.SaveChangesAsync();
                return Ok(new { message = "Contraseña actualizada correctamente." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "--- 🚨 ERROR CRÍTICO AL GUARDAR PASS ---");
                return StatusCode(500, new { message = "Error al procesar el cambio." });
            }
        }

        // 5. GESTIÓN Y ESTADÍSTICAS (Intactas)
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
            catch (Exception ex) { return StatusCode(500, new { message = "Error" }); }
        }

        [HttpPut("renovar/{id:guid}")]
        [Authorize]
        public async Task<IActionResult> RenovarSuscripcion(Guid id, [FromQuery] int meses = 1)
        {
            var usuario = await _context.usuarios.FindAsync(id);
            if (usuario == null) return NotFound();

            var fechaBase = (usuario.suscripcion_fin.HasValue && usuario.suscripcion_fin.Value > DateTime.UtcNow) 
                                ? usuario.suscripcion_fin.Value : DateTime.UtcNow;

            usuario.suscripcion_fin = fechaBase.AddMonths(meses);
            await _context.SaveChangesAsync();
            return Ok(new { message = "Suscripción extendida", nuevaFecha = usuario.suscripcion_fin });
        }

        // 6. CRUD BÁSICO...
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

        // 🚩 GENERAR TOKEN - AJUSTE KILLER: Añadimos ClienteId y ProveedorId a los Claims
        private string GenerarTokenJWT(Usuarios usuario, Guid? clienteId = null, Guid? proveedorId = null)
        {
            var jwtKey = _config["Jwt:Key"] ?? "Clave_Super_Secreta_2026_Turnify_Darwin";
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            
            var claims = new List<Claim> { 
                new Claim(ClaimTypes.NameIdentifier, usuario.id.ToString()), 
                new Claim(ClaimTypes.Name, usuario.nombre ?? ""), 
                new Claim(ClaimTypes.Role, usuario.Rol?.nombre ?? "Usuario") 
            };

            // 🛡️ Inyectamos los IDs reales en el Token
            if (clienteId.HasValue) claims.Add(new Claim("ClienteId", clienteId.Value.ToString()));
            if (proveedorId.HasValue) claims.Add(new Claim("ProveedorId", proveedorId.Value.ToString()));

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