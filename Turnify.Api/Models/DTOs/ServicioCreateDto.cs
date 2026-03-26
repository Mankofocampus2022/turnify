namespace Turnify.Api.Models.DTOs
{
    public class ServicioCreateDto
    {
        public Guid proveedor_id { get; set; }
        public string nombre { get; set; } = string.Empty;
        public string? descripcion { get; set; }
        public decimal precio { get; set; }
        public int duracion_minutos { get; set; }
    }
}