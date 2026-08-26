using LoanSystemAPI.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LoanSystemAPI.Controllers
{
    [ApiController]
    [Route("api/customers")]
    public class CustomersController : Controller
    {

        private readonly ILogger<CustomersController> _logger;
        private readonly AppDbContext _dbContext;

        public CustomersController( ILogger<CustomersController> logger, AppDbContext dbContext ) { 
        _logger = logger;
        _dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetAction()
        {

            _logger.LogInformation("ALL WORKING");
            return Ok("ALL WORKING");
        }


    }
}
