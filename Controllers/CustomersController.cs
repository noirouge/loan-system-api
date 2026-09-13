using LoanSystemAPI.Data;
using LoanSystemAPI.DTOs;
using LoanSystemAPI.Entities;
using LoanSystemAPI.Enums;
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

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CustomerDTO>>> GetCustomers()
        {
            try
            {
                var customers = await _dbContext.Customers.OrderByDescending(c => c.CreatedDate).ToListAsync();
                return Ok(customers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONSULTING CUSTOMERS");
                return StatusCode(500, new { message = "Error Consulting Customers" });
            }
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<CustomerDTO>> GetCustomer([FromRoute] Guid id )
        {
            try
            {
                var customer = await _dbContext.Customers.FirstOrDefaultAsync(c => c.Id == id);
             
                if (customer == null) return NotFound(new { message = $"The customer with the id: {id} was not found" });
                return Ok(customer);
            }
            catch (Exception ex) {
                _logger.LogError( ex, "ERROR FINDING CUSTOMER");
                return StatusCode(500, new {message = "ERROR FINDING CUSTOMER"});
            }     
        }

        [HttpPost]
        public async Task<IActionResult> PostCustomer([FromBody] CustomerRegisterDTO customer)
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

                return CreatedAtAction(nameof(GetCustomer), new {id = newCustomer.Id}, newCustomer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CREATING CUSTOMER");
                return StatusCode(500, new { message = "Could Not Created The Customer" });
            }
        }

        [HttpPut]
        public async Task<ActionResult<CustomerDTO>> PutCustomer([FromBody] CustomerDTO customerDTO)
        {

            try
            {
                var customer = await _dbContext.Customers.FirstOrDefaultAsync(c => c.Id == customerDTO.Id);
                if(customer == null)
                    return NotFound(new {message = $"The customer to update with id {customerDTO.Id} was not found" });
                customer.UpdatedBy = _adminId;
                customer.UpdatedDate = DateTime.UtcNow;
                customer.Note = customerDTO.Note;
                customer.Code = customerDTO.Code;
                customer.Phone = customerDTO.Phone;
                customer.Fullname = customerDTO.Fullname;
                await _dbContext.SaveChangesAsync();
                return Ok(customer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR UPDATING CUSTOMER");
                return StatusCode(500, new { message = "Could Not Updated The Customer" });
            }
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteCustomer([FromRoute] Guid id)
        {
            try
            {
                var customer = await _dbContext.Customers.FirstOrDefaultAsync(c => c.Id == id);
                if (customer == null)
                    return NotFound(new { message = $"The customer to delete with id {id} was not found" });
                customer.Status = CustomerStatus.DELETED;
                await _dbContext.SaveChangesAsync();
                return NoContent();
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Error Deleting Customer");
                return StatusCode(500, new { message = "Error Deleting Customer" });
            }
        }
    }
}
