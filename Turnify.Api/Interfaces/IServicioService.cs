using Turnify.Api.Models.DTOs;

namespace Turnify.Api.Interfaces
{
    public interface IServicioService
    {
        Task<IEnumerable<ServicioReadDto>> ObtenerTodos();
        Task<IEnumerable<ServicioReadDto>> ObtenerPorProveedor(Guid proveedorId);
        
        // 🚩 Agregamos esta línea para que coincida con el método extra que pusimos
        Task<IEnumerable<ServicioReadDto>> ObtenerActivosPorProveedor(Guid proveedorId);

        Task<ServicioReadDto?> ObtenerPorId(Guid id);

        // 🛡️ AJUSTE KILLER: Sincronizado con ServicioCreateDto (SnakeCase + JsonProperty)
        Task<ServicioReadDto> CrearServicio(ServicioCreateDto dto);

        // 🛡️ AJUSTE KILLER: Sincronizado con ServicioCreateDto
        Task<ServicioReadDto?> ActualizarServicio(Guid id, ServicioCreateDto dto);

        Task<bool> EliminarServicio(Guid id);
    }
}