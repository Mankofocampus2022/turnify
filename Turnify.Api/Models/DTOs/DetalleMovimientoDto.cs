namespace Turnify.Api.Models.DTOs
{
    public class DetalleMovimientoDto
    {
        public Guid CitaId { get; set; }
        public DateTime Fecha { get; set; }
        public string Cliente { get; set; } = string.Empty;
        public string Servicio { get; set; } = string.Empty;
        public string Especialista { get; set; } = string.Empty;
        public decimal MontoTotal { get; set; }
        public decimal PorcentajeComision { get; set; }
        public decimal MontoComisionEspecialista { get; set; }
        public decimal IngresoNeto { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string TipoModelo { get; set; } = string.Empty;
    }
}