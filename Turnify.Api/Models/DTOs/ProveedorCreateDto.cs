namespace Turnify.Api.Models.DTOs
{
    public class ProveedorCreateDto
    {
        public string nombre_comercial { get; set; } = string.Empty;
        public string direccion { get; set; } = string.Empty;
        public string tipo { get; set; } = string.Empty;
        public Guid usuarioId { get; set; }
        public bool trabaja_domicilio { get; set; } 
        public bool activo { get; set; } = true;
    }
}