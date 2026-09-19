using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.DTOs;

namespace Turnify.Api.Controllers
{
    [ApiController]
    [Route("api/v1/superadmin")]
    [Authorize(Roles = "SuperAdministrador,SuperAdmin,Administrador")]
    public class SuperAdminController : ControllerBase
    {
        private readonly TurnifyDbContext _context;

        public SuperAdminController(TurnifyDbContext context)
        {
            _context = context;
        }

       [HttpGet("metrics/subscriptions")]
        [HttpGet("/api/v1/admin/subscriptions/overview")]
        [Authorize(Roles = "SuperAdministrador,Administrador")]
        public async Task<ActionResult<SuperAdminMetricsResponseDto>> GetSubscriptionMetrics()
        
        {
            var now = DateTimeOffset.UtcNow;
            var alertThreshold = now.AddDays(7);

            var expiringQuery = await _context.suscripciones
                .Include(s => s.Proveedor)
                .Include(s => s.Plan)
                .Where(s => (s.Estado == "Activo" || s.Estado == "ACTIVO") && s.FechaVencimiento >= now && s.FechaVencimiento <= alertThreshold)
                .Select(s => new ExpiringSubscriptionDto
                {
                    SuscripcionId = s.Id,
                    ProveedorNombre = s.Proveedor.NombreComercial ?? "Sin Marca",
                    PlanNombre = s.Plan.Nombre,
                    FechaFin = s.FechaVencimiento,
                    DiasRestantes = (s.FechaVencimiento - now).Days
                })
                .ToListAsync();

            var totalRevenue = await _context.suscripciones
                .Where(s => s.Estado == "Activo" || s.Estado == "ACTIVO")
                .SumAsync(s => (decimal?)s.Plan.PrecioMensual) ?? 0m;

            var rolesDistribution = await _context.roles
                .Select(r => new RoleDistributionDto
                {
                    RolNombre = r.nombre,
                    TotalUsuarios = _context.usuarios.Count(u => u.rol_id == r.id)
                })
                .ToListAsync();

            var response = new SuperAdminMetricsResponseDto
            {
                TotalRevenue = totalRevenue,
                TotalActiveSubscriptions = await _context.suscripciones.CountAsync(s => s.Estado == "Activo" || s.Estado == "ACTIVO"),
                ExpiringSoonCount = expiringQuery.Count,
                ExpiringSubscriptions = expiringQuery,
                RolesDistribution = rolesDistribution
            };

            return Ok(response);
        }
    }
}