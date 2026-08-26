using LoanSystemAPI.Data;
using LoanSystemAPI.DTOs;
using LoanSystemAPI.Entities;
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
        private readonly IConfiguration _configuration;
// THIS IF FOR TESTING UNTIL LOGIN ARE AVAILABLE
        private readonly Guid _adminId;

        public CustomersController( ILogger<CustomersController> logger, AppDbContext dbContext, IConfiguration configuration ) { 
        _logger = logger;
        _dbContext = dbContext;
        _configuration = configuration;
            var configAdminId = _configuration["AdminId"] ?? throw new InvalidOperationException("NOT FOUND AdminId"); 
            if (Guid.TryParse(configAdminId, out Guid adminId))
            {
            _adminId = adminId; 
            }
        }

        [HttpPost]
        public async Task<IActionResult> PostCustomer([FromBody] CustomerDTO customer)
        {
            var newCustomer = new Customer {
                Fullname = customer.Fullname,
                Code = customer.Code,
                Phone = customer.Phone,
                Note = customer.Note,
                Id = Guid.NewGuid(),
                CreatedBy = _adminId,
            };
            try
            {
           await _dbContext.Customers.AddAsync(newCustomer);
           await _dbContext.SaveChangesAsync();

                return Created("", new {id = newCustomer.Id});
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message, "ERROR CREATING CUSTOMER");
                return StatusCode(500, new { message = "Could Not Created The Customer" });
            }
        }


    }
}
