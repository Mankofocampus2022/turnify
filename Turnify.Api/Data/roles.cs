using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Turnify.Api.Data;
using Turnify.Api.Models;

namespace Turnify.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RolesController : ControllerBase
    {
        private readonly TurnifyDbContext _context;

        public RolesController(TurnifyDbContext context)
        {
            _context = context;
        }

        // GET: api/roles
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Roles>>> GetRoles()
        {
            // Retorna la lista de roles desde la tabla 'roles'
            return await _context.roles.ToListAsync();
        }

        // GET: api/roles/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Roles>> GetRol(Guid id)
        {
            var rol = await _context.roles.FindAsync(id);

            if (rol == null)
            {
                return NotFound();
            }

            return rol;
        }
    }
}