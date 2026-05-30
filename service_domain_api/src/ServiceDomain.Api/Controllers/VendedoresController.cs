using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ServiceDomain.Core.Data;
using ServiceDomain.Core.Entities;

namespace ServiceDomain.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VendedoresController : ControllerBase
    {
        private readonly ServiceDomainDbContext _context;

        public VendedoresController(ServiceDomainDbContext context)
        {
            _context = context;
        }

        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard()
        {
            try
            {
                var totalClientes = await _context.Clientes.CountAsync();
                
                var encomendasVenda = _context.Encomendas
                    .Where(e => e.Tipo == DocumentoTipo.EncomendaCliente);

                var totalEncomendas = await encomendasVenda.CountAsync();
                var valorTotalVendas = await encomendasVenda.SumAsync(e => e.Total);

                var pendentesSync = await encomendasVenda.CountAsync(e => e.Status == "PendenteSync" || e.Status == "Pendente");
                var sincronizadas = await encomendasVenda.CountAsync(e => e.Status == "Sincronizado");
                var errosSync = await encomendasVenda.CountAsync(e => e.Status == "Erro");

                return Ok(new
                {
                    TotalClientes = totalClientes,
                    TotalEncomendas = totalEncomendas,
                    ValorTotalVendas = valorTotalVendas,
                    EncomendasPendentesSync = pendentesSync,
                    EncomendasSincronizadas = sincronizadas,
                    EncomendasErroSync = errosSync
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Erro ao carregar os dados do painel do vendedor.", Error = ex.Message });
            }
        }

        [HttpGet("encomendas")]
        public async Task<IActionResult> GetEncomendas()
        {
            try
            {
                var encomendas = await _context.Encomendas
                    .Include(e => e.Linhas)
                    .Where(e => e.Tipo == DocumentoTipo.EncomendaCliente)
                    .OrderByDescending(e => e.CreatedAt)
                    .ToListAsync();

                return Ok(encomendas);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "Erro ao carregar a lista de encomendas.", Error = ex.Message });
            }
        }
    }
}
