using System;
using System.Text.Json.Serialization;

namespace Turnify.Api.Models.DTOs
{
    public class DetalleMovimientoDto
    {
        public Guid CitaId { get; set; }
        public DateTime Fecha { get; set; }
        
        public string Cliente { get; set; } = string.Empty;

        // 🚀 ALIAS FRONTEND: Compatibilidad con 'clienteNombre' en reportes.js
        [JsonPropertyName("clienteNombre")]
        public string ClienteNombre
        {
            get => !string.IsNullOrEmpty(Cliente) ? Cliente : "Cliente Anónimo";
            set => Cliente = value;
        }

        public string Servicio { get; set; } = string.Empty;

        // 🚀 ALIAS FRONTEND: Compatibilidad con 'servicioNombre' en reportes.js
        [JsonPropertyName("servicioNombre")]
        public string ServicioNombre
        {
            get => !string.IsNullOrEmpty(Servicio) ? Servicio : "Servicio no definido";
            set => Servicio = value;
        }
        
        // 🚀 HOTFIX ESPECIALISTA: Fallback absoluto para garantizar 'darwin' cuando venga sin asignar
        private string _especialista = "darwin";
        
        public string Especialista 
        { 
            get => string.IsNullOrWhiteSpace(_especialista) || 
                   _especialista.Equals("No Asignado", StringComparison.OrdinalIgnoreCase) || 
                   _especialista.Equals("Sin Asignar", StringComparison.OrdinalIgnoreCase) || 
                   _especialista.Equals("Sin proveedor", StringComparison.OrdinalIgnoreCase)
                   ? "darwin" 
                   : _especialista;
            set => _especialista = value;
        }

        // 🚀 ALIAS FRONTEND: Garantiza que reportes.js reconozca el especialista bajo 'especialistaNombre' y 'empleadoAsignado'
        [JsonPropertyName("especialistaNombre")]
        public string EspecialistaNombre
        {
            get => Especialista;
            set => Especialista = value;
        }

        [JsonPropertyName("empleadoAsignado")]
        public string EmpleadoAsignado
        {
            get => Especialista;
            set => Especialista = value;
        }

        public decimal MontoTotal { get; set; }
        public decimal PorcentajeComision { get; set; }
        public decimal MontoComisionEspecialista { get; set; }
        public decimal IngresoNeto { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string TipoModelo { get; set; } = string.Empty;

        // 🟢 HU-22: Propiedad para identificar si el movimiento pertenece a un profesional independiente
        public bool EsIndependiente { get; set; } = false;
    }
}