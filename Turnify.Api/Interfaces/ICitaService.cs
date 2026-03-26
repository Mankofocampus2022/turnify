using Turnify.Api.Models.DTOs;

namespace Turnify.Api.Interfaces // El namespace debe coincidir con la carpeta
{
    public interface ICitaService
    {
        // Esto define qué métodos tendrá nuestro servicio
        Task<(bool Success, string Message, Guid? CitaId)> AgendarCitaAutomaticaAsync(CitaCreateDto dto);
        Task<IEnumerable<object>> GetAgendaDiaAsync(Guid proveedorId, DateTime fecha);
        Task<(bool Success, string Message)> UpdateEstadoCitaAsync(Guid id, string nuevoEstado);
        Task<IEnumerable<TimeSpan>> GetDisponibilidadAsync(Guid proveedorId, Guid servicioId, DateTime fecha);

        Task<IEnumerable<object>> GetHistorialClienteAsync(Guid clienteId);
    }
}