using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Models;
using Turnify.Api.Models.DTOs;
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authorization;
using Turnify.Api.Interfaces;
using System.Security.Claims;

namespace Turnify.Api.Controllers
{
    // 🚀 DTO para la carga de foto y creación/edición de colaboradores (HU-08 / HU-09)
    public class ProveedorFormDto
    {
        public Guid? Id { get; set; }
        public string NombreComercial { get; set; } = string.Empty;
        public string Direccion { get; set; } = string.Empty;
        public string Tipo { get; set; } = "negocio";
        public string? Categoria { get; set; } = "Barbero";
        public string? Telefono { get; set; }
        public string? Email { get; set; }
        public string? Password { get; set; } // Opcional: Para crear credenciales al dependiente (HU-09)
        public bool TrabajaDomicilio { get; set; } = false;
        public bool EsIndependiente { get; set; } = false;
        public decimal PorcentajeComision { get; set; } = 0.00m;
        public Guid? StaffId { get; set; }
        public Guid? UsuarioId { get; set; }
        public IFormFile? FotoFile { get; set; } // Carga de foto circular (HU-08)
    }

    [Route("api/[controller]")]
    [ApiController]
    public class ProveedoresController : ControllerBase
    {
        private readonly TurnifyDbContext _context;
        private readonly IStringLocalizer<Messages> _localizer;
        private readonly IUsuarioService _usuarioService;
        private readonly IWebHostEnvironment _env;

        public ProveedoresController(
            TurnifyDbContext context, 
            IStringLocalizer<Messages> localizer, 
            IUsuarioService usuarioService,
            IWebHostEnvironment env)
        {
            _context = context;
            _localizer = localizer;
            _usuarioService = usuarioService;
            _env = env;
        }

        [HttpGet("test-idioma")]
        public IActionResult TestIdioma()
        {
            var mensaje = _localizer["Welcome"]; 
            return Ok(new { respuesta = mensaje.Value });
        }

        // 🧠 [KILLER FIX QR] - Permitimos acceso anónimo para que clientes sin cuenta carguen la lista de locales
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<object>>> GetProveedores(
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 200, 
            [FromQuery] string? search = null,
            [FromQuery] bool ignorePagination = false)
        {
            var query = _context.proveedores
                .AsNoTracking()
                .Include(p => p.Usuario)
                .Where(p => !p.Eliminado);

            // 🚀 Filtrado dinámico por nombre comercial o categoría
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => (p.NombreComercial != null && p.NombreComercial.Contains(search)) || 
                                         (p.Categoria != null && p.Categoria.Contains(search)));
            }

            var finalQuery = query.OrderBy(p => p.NombreComercial);

            IQueryable<Proveedores> queryProcesada = finalQuery;
            if (!ignorePagination)
            {
                if (page <= 0) page = 1;
                if (pageSize <= 0) pageSize = 200;
                queryProcesada = finalQuery.Skip((page - 1) * pageSize).Take(pageSize);
            }

            return await queryProcesada
                .Select(p => new {
                    p.Id,
                    p.NombreComercial,
                    p.Direccion,
                    p.Tipo,
                    p.Categoria,
                    p.Telefono,
                    p.Email,
                    p.TrabajaDomicilio,
                    p.Activo,
                    
                    // 🚀 NUEVOS CAMPOS INYECTADOS (HU-08 a HU-12)
                    p.FotoUrl,
                    p.EsIndependiente,
                    p.StaffId,
                    p.PorcentajeComision,

                    Dueno = p.Usuario != null && p.Usuario.nombre != null ? p.Usuario.nombre : "Usuario no encontrado",

                    // RETROCOMPATIBILIDAD SINK
                    usuario_id = p.UsuarioId,
                    nombre_comercial = p.NombreComercial,
                    trabaja_domicilio = p.TrabajaDomicilio,
                    foto_url = p.FotoUrl,
                    es_independiente = p.EsIndependiente,
                    staff_id = p.StaffId,
                    porcentaje_comision = p.PorcentajeComision
                })
                .ToListAsync();
        }

        // 🧠 [KILLER FIX QR] - Acceso anónimo para detalle unitario
        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> GetProveedor(Guid id)
        {
            var proveedor = await _context.proveedores
                .AsNoTracking()
                .Include(p => p.Usuario)
                .Where(p => !p.Eliminado)
                .Select(p => new {
                    p.Id,
                    p.NombreComercial,
                    p.Direccion,
                    p.Tipo,
                    p.Categoria,
                    p.Telefono,
                    p.Email,
                    p.UsuarioId,
                    UsuarioNombre = p.Usuario != null ? p.Usuario.nombre : "N/A",
                    p.TrabajaDomicilio,
                    p.Activo,

                    // 🚀 NUEVOS CAMPOS (HU-08 a HU-12)
                    p.FotoUrl,
                    p.EsIndependiente,
                    p.StaffId,
                    p.PorcentajeComision,

                    // Espejo de compatibilidad
                    usuario_id = p.UsuarioId,
                    nombre_comercial = p.NombreComercial,
                    trabaja_domicilio = p.TrabajaDomicilio,
                    foto_url = p.FotoUrl,
                    es_independiente = p.EsIndependiente,
                    staff_id = p.StaffId,
                    porcentaje_comision = p.PorcentajeComision
                })
                .FirstOrDefaultAsync(p => p.Id == id);

            if (proveedor == null) return NotFound(new { message = "Proveedor no encontrado" });
            return proveedor;
        }

        // =========================================================================
        // 🚀 SUBIDA DE FOTO Y EDICIÓN/ACTUALIZACIÓN DE PERFIL (HU-08, HU-09, HU-12)
        // =========================================================================
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> UpdatePerfil(Guid id, [FromForm] ProveedorFormDto dto)
        {
            if (id != dto.Id && dto.Id.HasValue && dto.Id != Guid.Empty)
            {
                return BadRequest(new { message = "El ID de la URL no coincide con el del cuerpo." });
            }

            var proveedor = await _context.proveedores.FindAsync(id);
            if (proveedor == null)
            {
                return NotFound(new { message = "Proveedor no encontrado." });
            }

            // Actualización de campos básicos
            proveedor.NombreComercial = string.IsNullOrWhiteSpace(dto.NombreComercial) ? proveedor.NombreComercial : dto.NombreComercial;
            proveedor.Direccion = string.IsNullOrWhiteSpace(dto.Direccion) ? proveedor.Direccion : dto.Direccion;
            proveedor.Tipo = string.IsNullOrWhiteSpace(dto.Tipo) ? proveedor.Tipo : dto.Tipo;
            proveedor.Categoria = dto.Categoria ?? proveedor.Categoria;
            proveedor.Telefono = dto.Telefono ?? proveedor.Telefono;
            proveedor.Email = dto.Email ?? proveedor.Email;
            proveedor.TrabajaDomicilio = dto.TrabajaDomicilio;
            proveedor.EsIndependiente = dto.EsIndependiente;
            proveedor.PorcentajeComision = dto.PorcentajeComision;

            if (dto.StaffId.HasValue) proveedor.StaffId = dto.StaffId;

            // Procesamiento de Foto (HU-08 - CA3, CA4)
            if (dto.FotoFile != null && dto.FotoFile.Length > 0)
            {
                var fotoResult = await GuardarFotoServidor(dto.FotoFile);
                if (!fotoResult.Success)
                {
                    return BadRequest(new { message = fotoResult.Message });
                }
                proveedor.FotoUrl = fotoResult.Url;
            }

            // 🚀 Creación opcional de credenciales de acceso si se enviaron durante edición (HU-09)
            if (!string.IsNullOrWhiteSpace(dto.Email) && !string.IsNullOrWhiteSpace(dto.Password) && proveedor.UsuarioId == null)
            {
                var existeEmail = await _context.usuarios.AnyAsync(u => u.email.ToLower() == dto.Email.ToLower());
                if (existeEmail)
                {
                    return BadRequest(new { message = "El correo electrónico ya se encuentra registrado." });
                }

                var rolDependiente = await _context.roles.FirstOrDefaultAsync(r => r.nombre == Roles.RoleNames.ProveedorDependiente)
                                   ?? await _context.roles.FindAsync(Roles.RoleIds.ProveedorDependiente);

                var nuevoUsuario = new Usuarios
                {
                    id = Guid.NewGuid(),
                    nombre = dto.NombreComercial,
                    email = dto.Email,
                    password_hash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                    rol_id = rolDependiente?.id ?? Roles.RoleIds.ProveedorDependiente
                };

                _context.usuarios.Add(nuevoUsuario);
                proveedor.UsuarioId = nuevoUsuario.id;
            }

            proveedor.FechaActualizacion = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "¡Perfil actualizado con éxito!", fotoUrl = proveedor.FotoUrl });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "Error al guardar", 
                    details = ex.InnerException?.Message ?? ex.Message 
                });
            }
        }

        // =========================================================================
        // 🚀 CREACIÓN DE PROVEEDOR / EMPLEADO CON FOTO Y CREDENCIALES (HU-08, HU-09)
        // =========================================================================
        [HttpPost]
        public async Task<ActionResult<Proveedores>> PostProveedor([FromForm] ProveedorFormDto dto)
        {
            Guid? usuarioIdAsignado = dto.UsuarioId;

            // 🚀 HU-09: Si se ingresan Email y Contraseña opcionales, creamos credenciales de acceso
            if (!string.IsNullOrWhiteSpace(dto.Email) && !string.IsNullOrWhiteSpace(dto.Password))
            {
                var existeEmail = await _context.usuarios.AnyAsync(u => u.email.ToLower() == dto.Email.ToLower());
                if (existeEmail)
                {
                    // Mensaje de error exacto (HU-09 - CA3)
                    return BadRequest(new { message = "El correo electrónico ya se encuentra registrado." });
                }

                var rolDependiente = await _context.roles.FirstOrDefaultAsync(r => r.nombre == Roles.RoleNames.ProveedorDependiente)
                                   ?? await _context.roles.FindAsync(Roles.RoleIds.ProveedorDependiente);

                var nuevoUsuario = new Usuarios
                {
                    id = Guid.NewGuid(),
                    nombre = dto.NombreComercial,
                    email = dto.Email,
                    password_hash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                    rol_id = rolDependiente?.id ?? Roles.RoleIds.ProveedorDependiente
                };

                _context.usuarios.Add(nuevoUsuario);
                usuarioIdAsignado = nuevoUsuario.id;
            }

            // Procesar Foto (HU-08)
            string? fotoRelativaUrl = null;
            if (dto.FotoFile != null && dto.FotoFile.Length > 0)
            {
                var fotoResult = await GuardarFotoServidor(dto.FotoFile);
                if (!fotoResult.Success)
                {
                    return BadRequest(new { message = fotoResult.Message });
                }
                fotoRelativaUrl = fotoResult.Url;
            }

            // Identificar StaffId si la petición proviene de un Staff autenticado
            Guid? staffIdActual = dto.StaffId;
            var staffClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (staffIdActual == null && !string.IsNullOrEmpty(staffClaim) && User.IsInRole(Roles.RoleNames.Staff))
            {
                staffIdActual = Guid.Parse(staffClaim);
            }

            var nuevoProveedor = new Proveedores
            {
                Id = Guid.NewGuid(),
                NombreComercial = dto.NombreComercial,
                Direccion = string.IsNullOrWhiteSpace(dto.Direccion) ? "Local Comercial" : dto.Direccion,
                Tipo = dto.Tipo ?? "negocio",
                Categoria = dto.Categoria ?? "Barbero",
                UsuarioId = usuarioIdAsignado,
                Telefono = dto.Telefono ?? string.Empty,
                Email = dto.Email,
                TrabajaDomicilio = dto.TrabajaDomicilio,
                EsIndependiente = dto.EsIndependiente,
                StaffId = dto.EsIndependiente ? null : staffIdActual,
                PorcentajeComision = dto.EsIndependiente ? 0.00m : dto.PorcentajeComision,
                FotoUrl = fotoRelativaUrl,
                Activo = true,
                Eliminado = false,
                FechaCreacion = DateTime.UtcNow
            };

            _context.proveedores.Add(nuevoProveedor);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProveedor), new { id = nuevoProveedor.Id }, nuevoProveedor);
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteProveedor(Guid id)
        {
            var proveedor = await _context.proveedores.FindAsync(id);
            if (proveedor == null) return NotFound();

            proveedor.Eliminado = true;
            await _context.SaveChangesAsync();
            return Ok(new { mensaje = "Soft Delete realizado con éxito" });
        }

        // =========================================================================
        // 🛠️ MÉTODOS PRIVADOS DE APOYO (Validación y Guardado de Foto)
        // =========================================================================
        private async Task<(bool Success, string Message, string? Url)> GuardarFotoServidor(IFormFile file)
        {
            var extensionesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!extensionesPermitidas.Contains(ext))
            {
                return (false, "Formato de imagen no válido. Formatos permitidos: JPG, JPEG, PNG, WEBP.", null);
            }

            if (file.Length > 3 * 1024 * 1024) // 3MB (HU-08 - CA4)
            {
                return (false, "La foto de perfil no debe superar los 3 MB.", null);
            }

            var uploadsFolder = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", "proveedores");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var fileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return (true, "Éxito", $"/uploads/proveedores/{fileName}");
        }
    }
}