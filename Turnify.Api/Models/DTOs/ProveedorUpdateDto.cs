namespace Turnify.Api.Models.DTOs // 🚩 Agregamos .DTOs para mantener el orden
{
    public class ProveedorUpdateDto
    {
        // El ID es vital para el mapeo del PUT. 
        // Al ser Guid, .NET se encargará de validar que sea un ID real.
        public Guid Id { get; set; }
        
        public string NombreComercial { get; set; } = string.Empty;
        
        public string Direccion { get; set; } = string.Empty;
        
        public string Tipo { get; set; } = string.Empty;
    }
}