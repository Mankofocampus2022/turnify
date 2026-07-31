using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Turnify.Api.Data;
using Turnify.Api.Interfaces;
using Turnify.Api.Models.DTOs;

namespace Turnify.Api.Controllers
{
    [Authorize] // 🛡️ Todo el controlador protegido por JWT
    [Route("api/[controller]")]
    [ApiController]
    public class EstacionesTrabajoController : ControllerBase
    {
        private readonly IEstacionTrabajoService _estacionService;
        private readonly TurnifyDbContext _context;

        public EstacionesTrabajoController(IEstacionTrabajoService estacionService, TurnifyDbContext context)
        {
            _estacionService = estacionService;
            _context = context;
        }

        // 🧠 MÉTODO PRIVADO DE SEGURIDAD BLINDADO MULTINIVEL:
        // 1. Escaneo exhaustivo e insensible a mayúsculas de Claims en el JWT (Soporta ProveedorId, proveedorId, o URI schemas).
        // 2. Fallback por UsuarioId en la DB.
        // 3. Fallback por StaffId para usuarios de administración / búnker / colaboradores.
        private async Task<Guid?> GetProveedorIdAsync()
        {
            // Intento 1: Leer desde Claims del JWT (Case-Insensitive)
            var proveedorClaim = User.Claims.FirstOrDefault(c => 
                c.Type.Equals("ProveedorId", StringComparison.OrdinalIgnoreCase) || 
                c.Type.Equals("proveedorId", StringComparison.OrdinalIgnoreCase) ||
                c.Type.EndsWith("/ProveedorId", StringComparison.OrdinalIgnoreCase))?.Value;

            if (!string.IsNullOrEmpty(proveedorClaim) && Guid.TryParse(proveedorClaim, out Guid proveedorGuid))
            {
                return proveedorGuid;
            }

            // Intento 2: Fallback desde la DB con el NameIdentifier
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                            ?? User.FindFirst("nameidentifier")?.Value;

            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId)) 
            {
                return null;
            }

            // Buscar si es dueño de negocio directo (UsuarioId)
            var proveedor = await _context.proveedores.AsNoTracking().FirstOrDefaultAsync(p => p.UsuarioId == userId);
            if (proveedor != null) return proveedor.Id;

            // Buscar si está vinculado como Staff/Empleado
            var proveedorStaff = await _context.proveedores.AsNoTracking().FirstOrDefaultAsync(p => p.StaffId == userId);
            return proveedorStaff?.Id;
        }

        // 🪑 GET: api/estacionestrabajo
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var proveedorId = await GetProveedorIdAsync();
                if (proveedorId == null) 
                {
                    return Unauthorized(new { message = "No tienes un perfil de negocio registrado." });
                }

                var estaciones = await _estacionService.GetAllByProveedorAsync(proveedorId.Value);
                return Ok(estaciones);
            }
            catch (Exception ex)
            {
                var innerError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, new { message = "Error al obtener las estaciones de trabajo.", error = innerError });
            }
        }

        // 🔍 GET: api/estacionestrabajo/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            try
            {
                var proveedorId = await GetProveedorIdAsync();
                if (proveedorId == null) 
                {
                    return Unauthorized(new { message = "No tienes un perfil de negocio registrado." });
                }

                var estacion = await _estacionService.GetByIdAsync(id, proveedorId.Value);
                
                if (estacion == null) 
                {
                    return NotFound(new { message = "Estación de trabajo no encontrada o no pertenece a tu negocio." });
                }

                return Ok(estacion);
            }
            catch (Exception ex)
            {
                var innerError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, new { message = "Error al obtener la estación de trabajo.", error = innerError });
            }
        }

        // ➕ POST: api/estacionestrabajo
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] EstacionTrabajoCreateDto dto)
        {
            if (dto == null)
            {
                return BadRequest(new { message = "Los datos de la estación de trabajo son requeridos." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var proveedorId = await GetProveedorIdAsync();
                if (proveedorId == null) 
                {
                    return Unauthorized(new { message = "No tienes un perfil de negocio registrado." });
                }

                var nuevaEstacion = await _estacionService.CreateAsync(proveedorId.Value, dto);
                
                return CreatedAtAction(nameof(GetById), new { id = nuevaEstacion.Id }, nuevaEstacion);
            }
            catch (Exception ex)
            {
                var innerError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, new { message = "Error al crear la estación de trabajo.", error = innerError });
            }
        }

        // 📝 PUT: api/estacionestrabajo/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] EstacionTrabajoUpdateDto dto)
        {
            if (dto == null)
            {
                return BadRequest(new { message = "Los datos de actualización son requeridos." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var proveedorId = await GetProveedorIdAsync();
                if (proveedorId == null) 
                {
                    return Unauthorized(new { message = "No tienes un perfil de negocio registrado." });
                }

                var estacionActualizada = await _estacionService.UpdateAsync(id, proveedorId.Value, dto);
                
                if (estacionActualizada == null) 
                {
                    return NotFound(new { message = "Estación de trabajo no encontrada o no pertenece a tu negocio." });
                }

                return Ok(estacionActualizada);
            }
            catch (Exception ex)
            {
                var innerError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, new { message = "Error al actualizar la estación de trabajo.", error = innerError });
            }
        }

        // 🔄 PATCH: api/estacionestrabajo/{id}/toggle
        [HttpPatch("{id}/toggle")]
        public async Task<IActionResult> ToggleEstado(Guid id)
        {
            try
            {
                var proveedorId = await GetProveedorIdAsync();
                if (proveedorId == null) 
                {
                    return Unauthorized(new { message = "No tienes un perfil de negocio registrado." });
                }

                var success = await _estacionService.ToggleEstadoAsync(id, proveedorId.Value);
                
                if (!success) 
                {
                    return NotFound(new { message = "Estación de trabajo no encontrada o no pertenece a tu negocio." });
                }

                return Ok(new { message = "Estado de la estación modificado exitosamente." });
            }
            catch (Exception ex)
            {
                var innerError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, new { message = "Error al modificar el estado de la estación.", error = innerError });
            }
        }

        // =========================================================================
        // 💳 POST: api/estacionestrabajo/{id}/activar (HU-001-B: Activación/Pago Manual)
        // =========================================================================
        [HttpPost("{id}/activar")]
        public async Task<IActionResult> ActivarEstacion(Guid id, [FromBody] EstacionTrabajoActivarDto dto)
        {
            if (dto == null)
            {
                return BadRequest(new { message = "Los datos del pago y activación son requeridos." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                var proveedorId = await GetProveedorIdAsync();
                if (proveedorId == null) 
                {
                    return Unauthorized(new { message = "No tienes un perfil de negocio registrado." });
                }

                var estacionActualizada = await _estacionService.ActivarEstacionAsync(id, proveedorId.Value, dto);
                
                if (estacionActualizada == null) 
                {
                    return NotFound(new { message = "Estación de trabajo no encontrada o no pertenece a tu negocio." });
                }

                return Ok(new 
                { 
                    message = "¡Estación activada y pago registrado exitosamente!", 
                    estacion = estacionActualizada 
                });
            }
            catch (Exception ex)
            {
                var innerError = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, new { message = "Error al activar la estación de trabajo.", error = innerError });
            }
        }
    }
}