using Turnify.Api.Models.DTOs;

namespace Turnify.Api.Interfaces 
{
    public interface ICitaService
    {
        // 1. GESTIÓN DE CITAS Y DISPONIBILIDAD
        Task<(bool Success, string Message, Guid? CitaId)> AgendarCitaAutomaticaAsync(CitaCreateDto dto);
        Task<(bool Success, string Message)> UpdateEstadoCitaAsync(Guid id, string nuevoEstado);
        Task<IEnumerable<TimeSpan>> GetDisponibilidadAsync(Guid proveedorId, Guid servicioId, DateTime fecha);
        Task<IEnumerable<object>> GetHistorialClienteAsync(Guid clienteId);

        // 2. CONSULTAS DE AGENDA (Sincronizadas con el Controller)
        
        // 🚩 ESTE ES EL QUE CAUSABA EL ERROR CS1061:
        Task<IEnumerable<object>> GetAgendaDiaAsync(Guid proveedorId, DateTime fecha);
        
        // El método para el Dashboard inicial (Hoy)
        Task<IEnumerable<object>> GetAgendaHoyAsync(Guid userId);
        
        // El método potente para filtrar por Semana o Mes
        Task<IEnumerable<object>> GetCitasRangoAsync(Guid userId, DateTime inicio, DateTime fin);
    }
}