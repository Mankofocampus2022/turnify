namespace Turnify.Api.Models.DTOs
{
    public class HorarioAtencionDto
    {
        public int DiaSemana { get; set; } // 0-6
        public string HoraApertura { get; set; } = "09:00:00";
        public string HoraCierre { get; set; } = "20:00:00";
    }
}