namespace Turnify.Api.DTOs
{
    public class SuperAdminMetricsResponseDto
    {
        public decimal TotalRevenue { get; set; }
        public int TotalActiveSubscriptions { get; set; }
        public int ExpiringSoonCount { get; set; }
        public List<ExpiringSubscriptionDto> ExpiringSubscriptions { get; set; } = new();
        public List<RoleDistributionDto> RolesDistribution { get; set; } = new();
    }

    public class ExpiringSubscriptionDto
    {
        public Guid SuscripcionId { get; set; }
        public string ProveedorNombre { get; set; } = string.Empty;
        public string PlanNombre { get; set; } = string.Empty;
        public DateTimeOffset FechaFin { get; set; }
        public int DiasRestantes { get; set; }
    }

    public class RoleDistributionDto
    {
        public string RolNombre { get; set; } = string.Empty;
        public int TotalUsuarios { get; set; }
    }
}