namespace Turnify.Api.Models.DTOs
{
    public class CitaResponseDto
    {
        public Guid Id { get; set; }
        public TimeSpan Hora { get; set; }
        public string ClienteNombre { get; set; } = "Sin Nombre";
        public string ServicioNombre { get; set; } = "Sin Servicio";
        public decimal Precio { get; set; }
        public int Duracion { get; set; }
        public string Estado { get; set; } = "pendiente";
    }
}