using System;

namespace Turnify.Api.Models.DTOs
{
    /// <summary>
    /// DTO de respuesta para Citas. 
    /// Mantiene compatibilidad con nombres cortos y nombres técnicos del Service/Controller.
    /// </summary>
    public class CitaResponseDto
    {
        public Guid Id { get; set; }
        
        // 🕒 Temporalidad (Blindaje contra CS0117 en CitaService)
        public DateTime Fecha { get; set; } 
        public TimeSpan Hora { get; set; }
        
        // 👤 Identificación
        public string ClienteNombre { get; set; } = "Sin Nombre";
        public string ServicioNombre { get; set; } = "Sin Servicio";
        public string Estado { get; set; } = "pendiente";

        // 💰 Propiedades de compatibilidad (Nombres Cortos)
        public decimal Precio { get; set; } 
        public int Duracion { get; set; } 

        // 🛡️ Propiedades Requeridas por el Motor Overbooking PRO (Fix Errores CS0117/CS1061)
        // Se agregan explícitamente para que el .Select() en el Service no falle.
        public decimal PrecioPactado { get; set; }
        public int DuracionPactadaMin { get; set; }
        public string? Observaciones { get; set; }
        public string Modalidad { get; set; } = "local";
        public string? MetodoRegistro { get; set; }
        public string? Direccion { get; set; }
        public string? CodigoVerificacion { get; set; }
    }
}