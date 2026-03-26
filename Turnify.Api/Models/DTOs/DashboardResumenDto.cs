namespace Turnify.Api.Dtos
{
    public class DashboardResumenDto
    {
        public int TotalCitasHoy { get; set; }
        public int NuevosClientes { get; set; }
        public decimal IngresosMes { get; set; }
        
        // Esta lista es la que llenará la tabla de "Próximos Turnos"
        public List<CitaResumenDto> ProximasCitas { get; set; } = new();
    }

    public class CitaResumenDto
    {
        public string Cliente { get; set; } = string.Empty;
        public string Servicio { get; set; } = string.Empty;
        public string Hora { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
    }
}