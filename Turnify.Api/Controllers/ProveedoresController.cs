using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Models;
using Turnify.Api.Models.DTOs; // 🚩 Importante: Usaremos los DTOs que están en tu carpeta DTOs
using Microsoft.Extensions.Localization;
using Microsoft.AspNetCore.Authorization; // 🧠 INYECTADO SENIOR: Namespace necesario para liberar rutas públicas
using Turnify.Api.Interfaces; // 🧠 INYECTADO SENIOR: Requerido para acoplar el motor de paginación de usuarios

namespace Turnify.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProveedoresController : ControllerBase
    {
        private readonly TurnifyDbContext _context;
        private readonly IStringLocalizer<Messages> _localizer;
        private readonly IUsuarioService _usuarioService; // 🧠 INYECTADO SENIOR: Abstracción acoplada para mitigar la OBS-01

        public ProveedoresController(TurnifyDbContext context, IStringLocalizer<Messages> localizer, IUsuarioService usuarioService)
        {
            _context = context;
            _localizer = localizer;
            _usuarioService = usuarioService;
        }

        [HttpGet("test-idioma")]
        public IActionResult TestIdioma()
        {
            var mensaje = _localizer["Welcome"]; 
            return Ok(new { respuesta = mensaje.Value });
        }

        // 🧠 [KILLER FIX QR] - Permitimos acceso anónimo para que clientes sin cuenta carguen la lista de locales
        // 🚀 AJUSTE EFECTIVO OBS-01: Implementación del motor de filtrado y cortes limpios en base de datos
        // 🛠️ FIX BUG 2: Modificamos el pageSize por defecto a 200 para que se listen todos los negocios en el dropdown del front-end sin truncarse en 10.
        // 🛡️ BLINDAJE TOTAL: Agregamos [FromQuery] bool ignorePagination = false para que el Front-end pueda solicitar el listado completo sin cortes.
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<IEnumerable<object>>> GetProveedores(
            [FromQuery] int page = 1, 
            [FromQuery] int pageSize = 200, 
            [FromQuery] string? search = null,
            [FromQuery] bool ignorePagination = false)
        {
            // 🛡️ Inicializamos la consulta base como un IQueryable
            var query = _context.proveedores
                .AsNoTracking() // 🛡️ Evita el desborde de memoria RAM en el servidor productivo bloqueando el rastreo
                .Include(p => p.Usuario)
                .Where(p => !p.Eliminado);

            // 🚀 Filtrado dinámico en caliente por nombre comercial o categoría
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p => p.NombreComercial.Contains(search) || p.Categoria.Contains(search));
            }

            // 🚀 Ordenamos alfabéticamente por Nombre Comercial
            var finalQuery = query.OrderBy(p => p.NombreComercial);

            // 🚩 Si ignorePagination es true, se salta los cortes Skip/Take de forma limpia y segura
            IQueryable<Proveedores> queryProcesada = finalQuery;
            if (!ignorePagination)
            {
                if (page <= 0) page = 1;
                if (pageSize <= 0) pageSize = 200;
                queryProcesada = finalQuery.Skip((page - 1) * pageSize).Take(pageSize);
            }

            // 🚀 Execution of displacements (OFFSET / FETCH NEXT) mandatory indexed for SQL Server/Postgres
            return await queryProcesada
                .Select(p => new {
                    // 🚩 MANTENEMOS TUS PROPIEDADES ORIGINALES INTACTAS (Se serializan automáticamente a camelCase: id, nombreComercial, etc.)
                    p.Id,
                    p.NombreComercial,
                    p.Direccion,
                    p.Tipo,
                    p.Categoria,
                    p.Telefono,
                    p.Email,
                    p.TrabajaDomicilio,
                    p.Activo,
                    // 🛡️ FIX TRADUCCIÓN: Estandarizado con coalescencia nula para evitar excepciones HTTP 500 de LINQ
                    Dueno = p.Usuario.nombre ?? "Usuario no encontrado",

                    // 🛠️ RETROCOMPATIBILIDAD SEGURA: Inyectamos únicamente variantes snake_case que no colisionan con el mapeo camelCase
                    usuario_id = p.UsuarioId,
                    nombre_comercial = p.NombreComercial,
                    trabaja_domicilio = p.TrabajaDomicilio,
                    // dueno = p.Usuario.nombre ?? "Usuario no encontrado" // 🚩 FIX 500: Comentado para evitar que choque con 'Dueno' al serializar en camelCase
                })
                .ToListAsync();
        }

        [HttpPut("{id:guid}")] // 🚩 Agregamos :guid para validar la ruta de una vez
        public async Task<IActionResult> UpdatePerfil(Guid id, [FromBody] ProveedorUpdateDto dto)
        {
            // 1. Validación de consistencia
            if (id != dto.Id)
            {
                return BadRequest(new { message = "El ID de la URL no coincide con el del cuerpo." });
            }

            // 2. Buscar el registro
            var proveedor = await _context.proveedores.FindAsync(id);

            if (proveedor == null)
            {
                return NotFound(new { message = "Proveedor no encontrado." });
            }

            // 3. Mapeo Manual (Actualizamos solo lo permitido)
            proveedor.NombreComercial = dto.NombreComercial;
            proveedor.Direccion = dto.Direccion;
            proveedor.Tipo = dto.Tipo;
            
            // 🧠 INYECTADO SENIOR: Mapeo de actualización en caliente desde el panel administrativo
            proveedor.Categoria = dto.Categoria ?? proveedor.Categoria;

            // 🚩 KILLER FIX BUG DE ACTUALIZACIÓN: Forzamos la asignación de campos críticos
            // Se usa operador de coalescencia nula (??) para no sobreescribir con nulo si el Front-end no los envía
            proveedor.Telefono = dto.Telefono ?? proveedor.Telefono;
            proveedor.Email = dto.Email ?? proveedor.Email;

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "¡Perfil actualizado con exito!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { 
                    message = "Error al guardar", 
                    details = ex.InnerException?.Message ?? ex.Message 
                });
            }
        }

        // 🧠 [KILLER FIX QR] - Permitimos acceso anónimo para obtener el detalle unitario del negocio escaneado
        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> GetProveedor(Guid id)
        {
            var proveedor = await _context.proveedores
                .AsNoTracking()
                .Include(p => p.Usuario)
                .Where(p => !p.Eliminado)
                .Select(p => new {
                    // 🚩 MANTENEMOS TUS PROPIEDADES INTACTAS (PascalCase original)
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

                    // 🛠️ FIX BUG 1 UNITARIO: Espejo de propiedades seguras también para la consulta individual por ID
                    // 🚩 FIX 500: Comentamos los alias redundantes que colisionan con las propiedades estándar al serializarse a camelCase
                    // id = p.Id,
                    nombre_comercial = p.NombreComercial,
                    // nombreComercial = p.NombreComercial,
                    // direccion = p.Direccion,
                    // tipo = p.Tipo,
                    // categoria = p.Categoria,
                    // telefono = p.Telefono,
                    // email = p.Email,
                    // usuarioId = p.UsuarioId,
                    usuario_id = p.UsuarioId,
                    // usuarioNombre = p.Usuario != null ? p.Usuario.nombre : "N/A",
                    trabaja_domicilio = p.TrabajaDomicilio,
                    // activo = p.Activo
                })
                .FirstOrDefaultAsync(p => p.Id == id);

            if (proveedor == null) return NotFound(new { message = "Proveedor no encontrado" });
            return proveedor;
        }

        [HttpPost]
        public async Task<ActionResult<Proveedores>> PostProveedor([FromBody] ProveedorCreateDto dto)
        {
            var usuarioExiste = await _context.usuarios.AnyAsync(u => u.id == dto.usuarioId);
            if (!usuarioExiste) return BadRequest("El usuario dueño no existe.");

            var nuevoProveedor = new Proveedores
            {
                Id = Guid.NewGuid(),
                NombreComercial = dto.nombre_comercial,
                Direccion = dto.direccion,
                Tipo = dto.tipo,
                // 🧠 INYECTADO SENIOR: Captura el valor exacto enviado por el JSON del frontend, si falta cae en "Barbero"
                Categoria = dto.categoria ?? "Barbero",
                UsuarioId = dto.usuarioId,
                // 🚩 AGREGADO EN CREACIÓN: Capturamos los datos base desde el inicio
                Telefono = dto.telefono ?? string.Empty,
                Email = dto.email,
                FechaCreacion = DateTime.UtcNow, // Cambiado a UtcNow para estandarizar con Postgres
                TrabajaDomicilio = dto.trabaja_domicilio,
                Activo = dto.activo,
                Eliminado = false
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
    }
}