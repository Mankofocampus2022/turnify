using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Turnify.Api.Controllers
{
    [ApiController]
    [Route("api/health")]
    public class HealthController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public HealthController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet("db")]
        public async Task<IActionResult> CheckDatabase()
        {
            var connectionString = _configuration.GetConnectionString("DefaultConnection");

            try
            {
                using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync();

                using var command = new SqlCommand("SELECT 1", connection);
                await command.ExecuteScalarAsync();

                return Ok(new
                {
                    status = "OK",
                    database = "SQL Server",
                    time = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    status = "ERROR",
                    message = ex.Message
                });
            }
        }
    }
}
