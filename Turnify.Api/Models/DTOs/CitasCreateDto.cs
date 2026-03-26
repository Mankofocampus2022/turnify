namespace Turnify.Api.Models.DTOs
{
    public class CitaCreateDto
    {
        public Guid ClienteId { get; set; }
        public Guid ProveedorId { get; set; }
        public Guid ServicioId { get; set; }
        public DateTime Fecha { get; set; }
        public TimeSpan Hora { get; set; }
        public string Modalidad { get; set; } = "local";
        public string? Direccion { get; set; }
        public string? Observaciones { get; set; }
        
        // El precio y duración se pueden enviar desde el front 
        // o buscarlos en el ServicioService antes de guardar.
        public decimal PrecioPactado { get; set; }
        public int DuracionPactadaMin { get; set; }
    }
}