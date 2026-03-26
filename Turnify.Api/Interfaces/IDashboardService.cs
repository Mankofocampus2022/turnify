namespace Turnify.Api.Interfaces
{
    public interface IDashboardService
    {
        Task<object> GetResumenDiarioAsync(Guid proveedorId, DateTime? fecha);
    }
}