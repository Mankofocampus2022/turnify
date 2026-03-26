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
        Task<ServicioReadDto> CrearServicio(ServicioUpsertDto dto);
        Task<ServicioReadDto?> ActualizarServicio(Guid id, ServicioUpsertDto dto);
        Task<bool> EliminarServicio(Guid id);
    }
}