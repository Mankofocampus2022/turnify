using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.DTOs;
using Turnify.Api.Models;

namespace Turnify.Api.Controllers
{
    [ApiController]
    [Route("api/v1/admin")]
    [Authorize(Roles = "SuperAdministrador,Administrador")]
    public class AdminUsersController : ControllerBase
    {
        private readonly TurnifyDbContext _context;

        public AdminUsersController(TurnifyDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// HU-DIR01: Obtiene las cuentas globales de usuarios registradas en el sistema.
        /// </summary>
        [HttpGet("system-users")]
        public async Task<IActionResult> GetSystemUsers()
        {
            try
            {
                var now = DateTime.UtcNow;

                var usuarios = await _context.usuarios
                    .Include(u => u.Rol)
                    .AsNoTracking()
                    .ToListAsync();

                var proveedores = await _context.proveedores
                    .AsNoTracking()
                    .ToListAsync();

                var suscripciones = await _context.suscripciones
                    .Include(s => s.Plan)
                    .AsNoTracking()
                    .ToListAsync();

                var result = usuarios.Select(u =>
                {
                    var prov = proveedores.FirstOrDefault(p => p.UsuarioId == u.id);
                    var sub = prov != null ? suscripciones.FirstOrDefault(s => s.ProveedorId == prov.Id) : null;

                    DateTime? fechaVenc = null;
                    if (sub != null)
                    {
                        fechaVenc = sub.FechaVencimiento;
                    }
                    else if (u.suscripcion_fin.HasValue)
                    {
                        fechaVenc = u.suscripcion_fin.Value;
                    }

                    int diasRestantes = fechaVenc.HasValue ? (int)(fechaVenc.Value - now).TotalDays : 0;
                    bool estaActivo = (u.activo ?? true) && !(u.esta_bloqueado ?? false);

                    return new SystemUserDto
                    {
                        UserId = u.id,
                        Nombre = u.nombre ?? string.Empty,
                        Email = u.email ?? string.Empty,
                        Telefono = u.telefono ?? "No registrado",
                        RolNombre = u.Rol?.nombre ?? "Sin Rol",
                        ProveedorNombre = prov?.NombreComercial ?? "N/A",
                        PlanNombre = sub?.Plan?.Nombre ?? "Sin Plan",
                        EstadoSuscripcion = sub?.Estado ?? (estaActivo ? "Activo" : "Inactivo"),
                        FechaVencimiento = fechaVenc,
                        DiasRestantes = diasRestantes < 0 ? 0 : diasRestantes,
                        Activo = estaActivo
                    };
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "Error al consultar usuarios del sistema.",
                    error = ex.Message,
                    innerError = ex.InnerException?.Message
                });
            }
        }

        /// <summary>
        /// HU-DIR02: Asienta pagos manuales multicanal (Nequi, Daviplata, Consignación) y extiende la vigencia.
        /// </summary>
        [HttpPost("subscriptions/{userId}/manual-payment")]
        public async Task<IActionResult> RegisterManualPayment(Guid userId, [FromBody] ManualPaymentDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var usuario = await _context.usuarios.FirstOrDefaultAsync(u => u.id == userId);
            if (usuario == null) return NotFound(new { message = "El usuario especificado no existe." });

            bool estaActivo = (usuario.activo ?? true) && !(usuario.esta_bloqueado ?? false);
            if (!estaActivo) return Conflict(new { message = "No se pueden asentar pagos para un usuario inactivado o bloqueado." });

            var proveedor = await _context.proveedores.FirstOrDefaultAsync(p => p.UsuarioId == userId);
            if (proveedor == null)
            {
                proveedor = new Proveedores
                {
                    Id = Guid.NewGuid(),
                    UsuarioId = userId,
                    NombreComercial = usuario.nombre,
                    Email = usuario.email,
                    Activo = true
                };
                _context.proveedores.Add(proveedor);
                await _context.SaveChangesAsync();
            }

            var suscripcion = await _context.suscripciones.FirstOrDefaultAsync(s => s.ProveedorId == proveedor.Id);
            var now = DateTime.UtcNow;

            if (suscripcion == null)
            {
                var planBase = await _context.planes_suscripcion.FirstOrDefaultAsync();
                if (planBase == null)
                {
                    return BadRequest(new { message = "No existe ningún plan de suscripción configurado en la base de datos." });
                }

                suscripcion = new Suscripciones
                {
                    Id = Guid.NewGuid(),
                    ProveedorId = proveedor.Id,
                    PlanId = planBase.Id,
                    FechaInicio = now,
                    FechaVencimiento = now.AddDays(dto.ExtensionPeriodDays),
                    Estado = "Activo"
                };
                _context.suscripciones.Add(suscripcion);
            }
            else
            {
                var baseDate = suscripcion.FechaVencimiento > now ? suscripcion.FechaVencimiento : now;
                suscripcion.FechaVencimiento = baseDate.AddDays(dto.ExtensionPeriodDays);
                suscripcion.Estado = "Activo";
                _context.suscripciones.Update(suscripcion);
            }

            usuario.suscripcion_fin = suscripcion.FechaVencimiento;
            _context.usuarios.Update(usuario);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Pago manual registrado exitosamente.",
                nuevaFechaVencimiento = suscripcion.FechaVencimiento,
                estado = suscripcion.Estado
            });
        }

        /// <summary>
        /// HU-DIR02: Otorga días de cortesía/prórroga sobre el periodo de servicio.
        /// </summary>
        [HttpPatch("subscriptions/{userId}/extend-service")]
        public async Task<IActionResult> ExtendService(Guid userId, [FromBody] ExtendServiceDto dto)
        {
            if (dto.AdditionalDays <= 0) return BadRequest(new { message = "Los días de prórroga deben ser mayor a cero." });

            var usuario = await _context.usuarios.FirstOrDefaultAsync(u => u.id == userId);
            if (usuario == null) return NotFound(new { message = "Usuario no encontrado." });

            var proveedor = await _context.proveedores.FirstOrDefaultAsync(p => p.UsuarioId == userId);
            if (proveedor == null)
            {
                proveedor = new Proveedores
                {
                    Id = Guid.NewGuid(),
                    UsuarioId = userId,
                    NombreComercial = usuario.nombre,
                    Email = usuario.email,
                    Activo = true
                };
                _context.proveedores.Add(proveedor);
                await _context.SaveChangesAsync();
            }

            var suscripcion = await _context.suscripciones.FirstOrDefaultAsync(s => s.ProveedorId == proveedor.Id);
            var now = DateTime.UtcNow;

            if (suscripcion == null)
            {
                var planBase = await _context.planes_suscripcion.FirstOrDefaultAsync();
                if (planBase == null)
                {
                    return BadRequest(new { message = "No existe ningún plan de suscripción configurado en la base de datos." });
                }

                suscripcion = new Suscripciones
                {
                    Id = Guid.NewGuid(),
                    ProveedorId = proveedor.Id,
                    PlanId = planBase.Id,
                    FechaInicio = now,
                    FechaVencimiento = now.AddDays(dto.AdditionalDays),
                    Estado = "Activo"
                };
                _context.suscripciones.Add(suscripcion);
            }
            else
            {
                var baseDate = suscripcion.FechaVencimiento > now ? suscripcion.FechaVencimiento : now;
                suscripcion.FechaVencimiento = baseDate.AddDays(dto.AdditionalDays);
                suscripcion.Estado = "Activo";
                _context.suscripciones.Update(suscripcion);
            }

            usuario.suscripcion_fin = suscripcion.FechaVencimiento;
            _context.usuarios.Update(usuario);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = $"Prórroga de {dto.AdditionalDays} días otorgada exitosamente.",
                nuevaFechaVencimiento = suscripcion.FechaVencimiento
            });
        }

        /// <summary>
        /// HU-DIR02: Realiza la conmutación de estado (Soft Delete / Reactivación) del usuario.
        /// </summary>
        [HttpDelete("users/{userId}")]
        public async Task<IActionResult> SoftDeleteUser(Guid userId)
        {
            var usuario = await _context.usuarios.FirstOrDefaultAsync(u => u.id == userId);
            if (usuario == null) return NotFound(new { message = "El usuario especificado no existe." });

            bool actualmenteActivo = (usuario.activo ?? true) && !(usuario.esta_bloqueado ?? false);
            bool nuevoEstado = !actualmenteActivo;

            usuario.activo = nuevoEstado;
            usuario.esta_bloqueado = !nuevoEstado;
            _context.usuarios.Update(usuario);

            var proveedor = await _context.proveedores.FirstOrDefaultAsync(p => p.UsuarioId == userId);
            if (proveedor != null)
            {
                proveedor.Activo = nuevoEstado;
                _context.proveedores.Update(proveedor);

                var suscripcion = await _context.suscripciones.FirstOrDefaultAsync(s => s.ProveedorId == proveedor.Id);
                if (suscripcion != null)
                {
                    suscripcion.Estado = nuevoEstado ? "Activo" : "Cancelado";
                    _context.suscripciones.Update(suscripcion);
                }
            }

            await _context.SaveChangesAsync();

            string accionTexto = nuevoEstado ? "reactivada" : "desactivada (Soft Delete)";
            return Ok(new { message = $"Cuenta de usuario {accionTexto} correctamente." });
        }
    }
}