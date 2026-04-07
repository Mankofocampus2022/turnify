namespace Turnify.Api.Models
{
    public class ProveedorUpdateDto
    {
        // El ID es vital para el mapeo del PUT
        public Guid Id { get; set; }
        
        public string NombreComercial { get; set; } = string.Empty;
        
        public string Direccion { get; set; } = string.Empty;
        
        public string Tipo { get; set; } = string.Empty;

        // Puedes agregar Teléfono o Email si los necesitas
        // public string? Telefono { get; set; }
    }
}