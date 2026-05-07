using Turnify.Api.Models.DTOs;

namespace Turnify.Api.Interfaces 
{
    public interface ICitaService
    {
        // --- 📝 1. GESTIÓN DE AGENDAMIENTO ---
        
        /// <summary>
        /// Ageda una cita validando bloques de tiempo (Fix Overbooking PRO) y generando Token de seguridad.
        /// </summary>
        Task<(bool Success, string Message, Guid? CitaId)> AgendarCitaAutomaticaAsync(CitaCreateDto dto);

        /// <summary>
        /// Actualiza el estado de una cita (pendiente, confirmada, completada, cancelada, ausente).
        /// </summary>
        Task<(bool Success, string Message)> UpdateEstadoCitaAsync(Guid id, string nuevoEstado);

        /// <summary>
        /// [CRÍTICO] Valida la presencia física mediante el Token de 6 dígitos (Check-in).
        /// </summary>
        Task<(bool Success, string Message)> ConfirmarAsistenciaAsync(Guid citaId, string token);


        // --- 🕒 2. MOTOR DE DISPONIBILIDAD ---
        
        /// <summary>
        /// Calcula slots libres validando que el servicio solicitado quepa completo en la agenda.
        /// </summary>
        Task<IEnumerable<TimeSpan>> GetDisponibilidadAsync(Guid proveedorId, Guid servicioId, DateTime fecha);


        // --- 📊 3. CONSULTAS Y REPORTES (Dashboard & BI) ---
        
        /// <summary>
        /// Obtiene la agenda específica de un día (Fix Reportes "Hoy").
        /// </summary>
        Task<IEnumerable<object>> GetAgendaDiaAsync(Guid proveedorId, DateTime fecha);
        
        /// <summary>
        /// Obtiene la agenda del día actual para el dashboard del profesional.
        /// </summary>
        Task<IEnumerable<object>> GetAgendaHoyAsync(Guid userId);
        
        /// <summary>
        /// Obtiene citas en un rango de fechas para reportes semanales, mensuales o analítica avanzada.
        /// </summary>
        Task<IEnumerable<object>> GetCitasRangoAsync(Guid userId, DateTime inicio, DateTime fin);

        /// <summary>
        /// Recupera el historial completo de citas de un cliente (Blindado para Privacidad).
        /// </summary>
        Task<IEnumerable<object>> GetHistorialClienteAsync(Guid clienteId);
    }
}