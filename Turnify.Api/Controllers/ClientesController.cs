using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Models;
using Turnify.Api.Models.DTOs;
using Turnify.Api.Interfaces;  

namespace Turnify.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ClientesController : ControllerBase
    {
        private readonly IClienteService _clienteService;

        public ClientesController(IClienteService clienteService)
        {
            _clienteService = clienteService;
        }

        [HttpGet("buscar/{telefono}")]
        public async Task<IActionResult> GetClientePorTelefono(string telefono)
        {
            var cliente = await _clienteService.GetClientePorTelefonoAsync(telefono);
            if (cliente == null) return NotFound("Cliente no registrado.");
            return Ok(cliente);
        }

        [HttpPost("registrar")]
        public async Task<IActionResult> PostCliente(ClienteCreateDto dto)
        {
            // 1. Llamamos al servicio usando el DTO
            var result = await _clienteService.RegistrarClienteAsync(dto);

            // 2. Si algo sale mal (ej. teléfono duplicado), avisamos
            if (!result.Success) 
            {
                return BadRequest(new { message = result.Message });
            }

            // 3. Si todo sale bien, devolvemos el objeto creado
            return Ok(new 
            { 
                message = result.Message, 
                cliente = result.Cliente 
            });
        }

        
        // Ahora el controlador solo llama al servicio y no toca las propiedades directamente
        [HttpGet("{clienteId}/mis-citas")]
        public async Task<IActionResult> GetMisCitas(Guid clienteId)
        {
            var citas = await _clienteService.GetMisCitasAsync(clienteId);
            return Ok(citas);
        }
    }
}