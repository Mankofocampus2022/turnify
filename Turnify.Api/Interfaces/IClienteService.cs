using Turnify.Api.Models;
using Turnify.Api.Models.DTOs;

namespace Turnify.Api.Interfaces
{
    public interface IClienteService
    {
        // El método nuevo ahora está dentro de la "caja" correcta
        Task<IEnumerable<Clientes>> GetClientesByUsuarioAsync(Guid usuarioId);
        
        Task<Clientes?> GetClientePorTelefonoAsync(string telefono);
        Task<(bool Success, string Message, Clientes? Cliente)> RegistrarClienteAsync(ClienteCreateDto dto);
        Task<IEnumerable<Clientes>> GetClientesAsync(string? search);
        Task<IEnumerable<object>> GetMisCitasAsync(Guid clienteId);

        // 🚀 [NUEVO] Firma matriculada para mitigar la OBS-01 (Paginación de alto rendimiento)
        // Evita que el compilador tire el error CS1061 al compilar el controlador
        Task<IEnumerable<Clientes>> GetClientesPaginadosAsync(int page, int pageSize, string? search);
    }
}