using System;
using System.Text.Json.Serialization;

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
        
        // 🚩 NUEVO: Nombre comercial o personal del proveedor profesional (Barbería/Establecimiento)
        public string ProveedorNombre { get; set; } = "Sin Proveedor";

        // 🚀 HU 001 / HOTFIX ESPECIALISTA: Fallback optimizado para garantizar 'darwin' ante nulos o no asignados
        private string _empleadoAsignado = "darwin";
        public string EmpleadoAsignado
        {
            get => string.IsNullOrWhiteSpace(_empleadoAsignado) || 
                   _empleadoAsignado.Equals("Sin Asignar", StringComparison.OrdinalIgnoreCase) ||
                   _empleadoAsignado.Equals("No Asignado", StringComparison.OrdinalIgnoreCase) ||
                   _empleadoAsignado.Equals("Especialista Asignado", StringComparison.OrdinalIgnoreCase) ||
                   _empleadoAsignado.Equals("Sin Proveedor", StringComparison.OrdinalIgnoreCase)
                   ? "darwin" 
                   : _empleadoAsignado;
            set => _empleadoAsignado = value;
        }

        // 🚀 ALIAS FRONTEND: Garantiza compatibilidad con scripts que consumen 'especialistaNombre' o 'especialista'
        [JsonPropertyName("especialistaNombre")]
        public string EspecialistaNombre => EmpleadoAsignado;

        [JsonPropertyName("especialista")]
        public string Especialista => EmpleadoAsignado;

        public string EstacionAsignada { get; set; } = "Local";
        
        public string Estado { get; set; } = "pendiente";

        // 🟢 HU-22: Bandera para identificar si la cita corresponde a un profesional independiente
        public bool EsIndependiente { get; set; } = false;

        // 💰 Propiedades de compatibilidad con sincronización inteligente (Nombres Cortos para Frontend)
        private decimal _precio;
        public decimal Precio 
        { 
            get => _precio != 0 ? _precio : PrecioPactado;
            set => _precio = value;
        }

        private int _duracion;
        public int Duracion 
        { 
            get => _duracion != 0 ? _duracion : DuracionPactadaMin;
            set => _duracion = value;
        }

        // 🛡️ Propiedades Requeridas por el Motor Overbooking PRO (Fix Errores CS0117/CS1061)
        private decimal _precioPactado;
        public decimal PrecioPactado 
        { 
            get => _precioPactado != 0 ? _precioPactado : _precio;
            set => _precioPactado = value;
        }

        private int _duracionPactadaMin;
        public int DuracionPactadaMin 
        { 
            get => _duracionPactadaMin != 0 ? _duracionPactadaMin : _duracion;
            set => _duracionPactadaMin = value;
        }

        public string? Observaciones { get; set; }
        public string Modalidad { get; set; } = "local";
        public string? MetodoRegistro { get; set; }
        
        // 📍 Ubicación y Domicilio (Añadido para completitud 100%)
        public string? Direccion { get; set; }
        public double? Latitud { get; set; }
        public double? Longitud { get; set; }
        public decimal? CostoDomicilio { get; set; }

        // 🔑 Seguridad Física (Check-in Token)
        // Este campo es el que permite al cliente ver su código en "Mis Citas"
        public string? CodigoVerificacion { get; set; }
    }
}