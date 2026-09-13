using LoanSystemAPI.Data;
using LoanSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace LoanSystemAPI.Controllers
{

    [ApiController]
    [Route("api/loans")]
    public class LoansController : Controller
    {
        private readonly ILogger<LoansController> _logger;
        private readonly AppDbContext _dbContext;
        private readonly ICurrentUserService _currentUserService;

        public LoansController(ILogger<LoansController> logger, AppDbContext dbContext, ICurrentUserService currentUserService)
        {
            _logger = logger;
            _dbContext = dbContext;
            _currentUserService = currentUserService;
        }

        

    }
}
