namespace Turnify.Api.Models.DTOs
{
    public class ServicioUpsertDto
    {
        public string Nombre { get; set; } = string.Empty;
        public string? Descripcion { get; set; }
        public decimal Precio { get; set; }
        public int DuracionMinutos { get; set; }
        public string Categoria { get; set; } = string.Empty;
        public Guid ProveedorId { get; set; }
        public decimal ComisionPorcentaje { get; set; }
        public string? ImagenUrl { get; set; }
        public int Activo { get; set; } // 🚩 Debe ser int
    }
}