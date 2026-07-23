namespace Turnify.Api.Dtos
{
    // =========================================================================
    // DTOs ORIGINALES (INPACTADOS - SE MANTIENEN 100% FUNCIONALES)
    // =========================================================================
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

    // =========================================================================
    // 💈 HU-06 & HU-07: NUEVOS DTOs PARA EL PROFESIONAL INDEPENDIENTE
    // =========================================================================

    /// <summary>
    /// Respuesta principal estructurada para el Dashboard del Profesional Independiente.
    /// Contiene totales de ingresos 100% brutos (HU-06) y métricas de nuevos clientes (HU-07).
    /// </summary>
    public class DashboardIndependienteDto
    {
        public string TipoResumen { get; set; } = string.Empty;
        public string RangoBusqueda { get; set; } = string.Empty;
        public int TotalCitas { get; set; }
        
        // HU-06: Cálculos de ingresos 100% brutos (sin deducciones de comisiones)
        public decimal IngresosProyectadosBrutos { get; set; }
        public decimal IngresosRealesBrutos { get; set; }

        // HU-07 CA2: Métrica de nuevos clientes en el periodo
        public int TotalNuevosClientes { get; set; }

        // Detalle de la agenda/citas para la tabla del frontend
        public List<CitaIndependienteDto> Citas { get; set; } = new();
    }

    /// <summary>
    /// Detalle de cada cita en la agenda del Profesional Independiente.
    /// Incluye la distinción explícita para saber si el cliente es nuevo o habitual (HU-07 CA1).
    /// </summary>
    public class CitaIndependienteDto
    {
        public Guid Id { get; set; }
        public string Hora { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public string Cliente { get; set; } = string.Empty;
        public string Servicio { get; set; } = string.Empty;
        public decimal PrecioPactado { get; set; }
        public string Estado { get; set; } = string.Empty;
        public string? CodigoVerificacion { get; set; }

        // HU-07 CA1: Flag que distingue si es un cliente nuevo (sin historial previo con el profesional)
        public bool EsNuevoCliente { get; set; }
    }
}