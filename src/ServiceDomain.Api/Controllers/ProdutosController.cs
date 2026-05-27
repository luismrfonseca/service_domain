using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDomain.Core.Data;

namespace ServiceDomain.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProdutosController : ControllerBase
    {
        private readonly ServiceDomainDbContext _context;

        public ProdutosController(ServiceDomainDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetProdutos([FromQuery] string? searchRef, [FromQuery] string? searchDesignacao)
        {
            var query = _context.Produtos.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchRef))
            {
                query = query.Where(p => p.Ref.Contains(searchRef));
            }

            if (!string.IsNullOrWhiteSpace(searchDesignacao))
            {
                query = query.Where(p => p.Designacao.Contains(searchDesignacao));
            }

            var produtos = await query.ToListAsync();
            return Ok(produtos);
        }
    }
}
