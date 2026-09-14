using LoanSystemAPI.Data;
using LoanSystemAPI.DTOs;
using LoanSystemAPI.Entities;
using LoanSystemAPI.Enums;
using LoanSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LoanSystemAPI.Controllers
{

    [ApiController]
    [Route("api")]
    public class FreezesController : Controller
    {
        private readonly ILogger<FreezesController> _logger;
        private readonly AppDbContext _dbContext;
        private readonly ICurrentUserService _currentUserService;
        private readonly LocalDateService _localDateService;

        public FreezesController(ILogger<FreezesController> logger, AppDbContext dbContext, ICurrentUserService currentUserService, LocalDateService localDateService)
        {
            _logger = logger;
            _dbContext = dbContext;
            _currentUserService = currentUserService;
            _localDateService = localDateService;
        }

        // A FREEZE PAUSES THE MONTHLY INTEREST OF A LOAN. IT ONLY LOOKS FORWARD: THE CHARGES ALREADY CREATED STAY (D-060)
        [HttpPost("loans/{loanId:guid}/freezes")]
        public async Task<IActionResult> PostFreeze([FromRoute] Guid loanId, [FromBody] FreezeCreateDTO freezeDTO)
        {
            if (freezeDTO.StartDate > _localDateService.Today())
                return BadRequest(new { message = "The freeze start date cannot be in the future" });

            try
            {
                var loan = await _dbContext.Loans.FirstOrDefaultAsync(l => l.Id == loanId);
                if (loan == null)
                    return NotFound(new { message = $"The loan with id {loanId} was not found" });

                // A CLOSED OR WRITTEN OFF LOAN DOES NOT GENERATE INTEREST, SO THERE IS NOTHING TO PAUSE
                if (loan.Status != LoanStatus.ACTIVE)
                    return BadRequest(new { message = "Only an active loan can be frozen" });

                if (freezeDTO.StartDate < loan.LoanDate)
                    return BadRequest(new { message = "The freeze start date cannot be earlier than the loan date" });

                var freeze = new Freeze
                {
                    Id = Guid.NewGuid(),
                    LoanId = loan.Id,
                    StartDate = freezeDTO.StartDate,
                    Reason = freezeDTO.Reason,
                    // UNTIL THE LOGIN EXISTS, WHO AUTHORIZES THE FREEZE IS THE SAME USER THAT REGISTERS IT
                    AuthorizedBy = _currentUserService.UserId,
                    Status = FreezeStatus.ACTIVE,
                    CreatedBy = _currentUserService.UserId,
                    CreatedDate = DateTime.UtcNow,
                };
                await _dbContext.Freezes.AddAsync(freeze);

                try
                {
                    await _dbContext.SaveChangesAsync();
                }
                // THE PARTIAL UNIQUE INDEX ALLOWS ONLY ONE OPEN FREEZE PER LOAN, EVEN WITH TWO REQUESTS AT THE SAME TIME
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_freezes_if_open" })
                {
                    return Conflict(new { message = $"The loan with id {loanId} already has an open freeze" });
                }

                return StatusCode(StatusCodes.Status201Created, new { id = freeze.Id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LOAN FREEZE ERROR");
                return StatusCode(500, new { message = "ERROR COULD NOT SAVED THE LOAN FREEZE" });
            }
        }
    }
}
