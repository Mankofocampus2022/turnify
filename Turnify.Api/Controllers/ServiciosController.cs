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
    // [Authorize] 
    public class ServiciosController : ControllerBase
    {
        private readonly IServicioService _servicioService;

        public ServiciosController(IServicioService servicioService)
        {
            _servicioService = servicioService;
        }

        // 🚩 Versión 1 de GetAll
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var servicios = await _servicioService.ObtenerTodos(); 
            return Ok(servicios);
        }

        // 🚩 Obtener UN servicio por ID
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var servicio = await _servicioService.ObtenerPorId(id);
            if (servicio == null) return NotFound();
            return Ok(servicio);
        }

        /* MIRA DARWIN: Este bloque de abajo es el que causaba el error CS0111 
           porque es idéntico al de arriba. Lo dejo aquí pero comentado para 
           que no te de error al compilar, tal como pediste de no borrar nada.
        */
        /*
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var servicios = await _servicioService.ObtenerTodos(); 
            return Ok(servicios);
        }
        */

        // 1. Obtener servicios de un proveedor específico
        [HttpGet("proveedor/{proveedorId}")]
        public async Task<IActionResult> GetByProveedor(Guid proveedorId)
        {
            var servicios = await _servicioService.ObtenerPorProveedor(proveedorId);
            return Ok(servicios);
        }

        // 2. Crear un nuevo servicio
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ServicioUpsertDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var resultado = await _servicioService.CrearServicio(dto);
            return CreatedAtAction(nameof(GetById), new { id = resultado.Id }, resultado);
        }

        // 3. ACTUALIZAR
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] ServicioUpsertDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            
            var actualizado = await _servicioService.ActualizarServicio(id, dto);
            if (actualizado == null) return NotFound("El servicio no existe.");
            
            return Ok(actualizado);
        }

        // 4. ELIMINAR (Borrado Lógico)
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var eliminado = await _servicioService.EliminarServicio(id);
            if (!eliminado) return NotFound("No se pudo eliminar el servicio.");
            
            return NoContent();
        }
    }
}