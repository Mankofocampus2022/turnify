using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Turnify.Api.Models.DTOs;

namespace Turnify.Api.Interfaces 
{
    /// <summary>
    /// Interfaz de servicios para la gestión de citas.
    /// Define las reglas de negocio para el motor de disponibilidad y validación de seguridad.
    /// </summary>
    public interface ICitaService
    {
        // --- 📝 1. GESTIÓN DE AGENDAMIENTO ---
        
        /// <summary>
        /// Ageda una cita validando bloques de tiempo (Fix Overbooking PRO) y generando Token de seguridad.
        /// 🛡️ Ahora soporta mapeo automático de UsuarioId a ClienteId.
        /// 🚀 HU 001: Soporta inyección de EmpleadoId y EstacionId mediante el DTO.
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
        /// Calcula slots libres validando que el servicio solicitado (servicioId) quepa completo 
        /// en el "túnel de tiempo" del proveedor en la fecha solicitada.
        /// </summary>
        Task<IEnumerable<TimeSpan>> GetDisponibilidadAsync(Guid proveedorId, Guid servicioId, DateTime fecha);


        // --- 📊 3. CONSULTAS Y REPORTES (Dashboard & BI) ---
        
        /// <summary>
        /// Obtiene la agenda específica de un día (Fix Reportes "Hoy").
        /// Blindado con CitaResponseDto para evitar exposición de datos sensibles.
        /// </summary>
        Task<IEnumerable<CitaResponseDto>> GetAgendaDiaAsync(Guid proveedorId, DateTime fecha);
        
        /// <summary>
        /// Obtiene la agenda del día actual para el dashboard del profesional.
        /// 🛡️ Soporta consulta por UsuarioId para negocios multi-perfil.
        /// </summary>
        Task<IEnumerable<CitaResponseDto>> GetAgendaHoyAsync(Guid userId);
        
        /// <summary>
        /// Obtiene citas en un rango de fechas para reportes semanales, mensuales o analítica avanzada.
        /// 🛡️ Mapea identidades de Proveedor y Cliente automáticamente.
        /// </summary>
        Task<IEnumerable<CitaResponseDto>> GetCitasRangoAsync(Guid userId, DateTime inicio, DateTime fin);

        /// <summary>
        /// Recupera el historial completo de citas de un cliente (Blindado para Privacidad).
        /// </summary>
        Task<IEnumerable<CitaResponseDto>> GetHistorialClienteAsync(Guid clienteId);

        // --- 📈 4. ANALÍTICA (MÉTODOS DE SOPORTE) ---

        /// <summary>
        /// 🚩 [NUEVO] Obtiene datos para gráficas de torta (Completadas vs Pendientes vs Canceladas).
        /// Necesario para el dashboard administrativo y de barbero.
        /// </summary>
        Task<object> GetEstadisticasTortaAsync(Guid proveedorId);
    }
}