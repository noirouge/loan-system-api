using LoanSystemAPI.Data;
using Microsoft.AspNetCore.Mvc;

namespace LoanSystemAPI.Controllers
{

    [ApiController]
    [Route("api/loans")]
    public class LoansController : Controller
    {
        private readonly ILogger<CustomersController> _logger;
        private readonly AppDbContext _dbContext;
        private readonly IConfiguration _configuration;
        // THIS IF FOR TESTING UNTIL LOGIN ARE AVAILABLE
        private readonly Guid _adminId;

        public LoansController(ILogger<CustomersController> logger, AppDbContext dbContext, IConfiguration configuration)
        {
            _logger = logger;
            _dbContext = dbContext;
            _configuration = configuration;
            var configAdminId = _configuration["AdminId"] ?? throw new InvalidOperationException("NOT FOUND AdminId");
            if (Guid.TryParse(configAdminId, out Guid adminId))
            {
                _adminId = adminId;
            }
        }

        

    }
}
