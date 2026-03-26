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
    }
}