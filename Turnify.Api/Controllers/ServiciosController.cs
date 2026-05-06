using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Models;
using Turnify.Api.Models.DTOs;
using Turnify.Api.Interfaces;

namespace Turnify.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    // [Authorize] // Mantengo el comentario como pediste
    public class ServiciosController : ControllerBase
    {
        private readonly IServicioService _servicioService;

        public ServiciosController(IServicioService servicioService)
        {
            _servicioService = servicioService;
        }

        // --- 🚩 OBTENER TODOS ---
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var servicios = await _servicioService.ObtenerTodos(); 
            return Ok(servicios);
        }

        // --- 🚩 OBTENER POR ID (Blindado con FromRoute) ---
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById([FromRoute] Guid id) 
        {
            var servicio = await _servicioService.ObtenerPorId(id);
            if (servicio == null) return NotFound(new { message = "Servicio no encontrado." });
            return Ok(servicio);
        }

        /* Bloque respetado (comentado por error CS0111 previo) */
        /*
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var servicios = await _servicioService.ObtenerTodos(); 
            return Ok(servicios);
        }
        */

        // --- 🚩 SERVICIOS POR PROVEEDOR ---
        [HttpGet("proveedor/{proveedorId}")]
        public async Task<IActionResult> GetByProveedor([FromRoute] Guid proveedorId)
        {
            var servicios = await _servicioService.ObtenerPorProveedor(proveedorId);
            return Ok(servicios);
        }

        // --- 🚩 CREAR SERVICIO (Blindaje corregido) ---
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ServicioCreateDto dto) // 🚩 Cambiado a ServicioCreateDto para coincidir con tu JS
        {
            // 🛡️ BLINDAJE: Si el modelo no es válido, extraemos los errores para el JS
            if (!ModelState.IsValid) 
            {
                return BadRequest(new { 
                    message = "Error de validación al crear servicio.",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                });
            }

            try 
            {
                var resultado = await _servicioService.CrearServicio(dto);
                return CreatedAtAction(nameof(GetById), new { id = resultado.Id }, resultado);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error interno: " + ex.Message });
            }
        }

        // --- 🚩 ACTUALIZAR (Aquí es donde suele saltar el 400) ---
        [HttpPut("{id}")]
        public async Task<IActionResult> Update([FromRoute] Guid id, [FromBody] ServicioCreateDto dto) // 🚩 Sincronizado con el DTO
        {
            // 🛡️ BLINDAJE SENIOR: Verificamos que el modelo sea válido antes de procesar
            if (!ModelState.IsValid) 
            {
                return BadRequest(new { 
                    message = "Error de validación en los datos enviados.",
                    errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
                });
            }
            
            var actualizado = await _servicioService.ActualizarServicio(id, dto);
            if (actualizado == null) return NotFound(new { message = "El servicio no existe o no pudo ser actualizado." });
            
            return Ok(actualizado);
        }

        // --- 🚩 ELIMINAR ---
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete([FromRoute] Guid id)
        {
            var eliminado = await _servicioService.EliminarServicio(id);
            if (!eliminado) return NotFound(new { message = "No se pudo eliminar el servicio." });
            
            return NoContent();
        }
    }
}