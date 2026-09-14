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

        // A FREEZE IS CLOSED BY WRITING ITS END DATE, NOT BY CHANGING ITS STATUS
        [HttpPost("freezes/{id:guid}/close")]
        public async Task<IActionResult> CloseFreeze([FromRoute] Guid id, [FromBody] FreezeCloseDTO closeDTO)
        {
            if (closeDTO.EndDate > _localDateService.Today())
                return BadRequest(new { message = "The freeze end date cannot be in the future" });

            try
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync();

                // LOCKS THE FREEZE ROW UNTIL THE COMMIT: A SECOND CLOSE AT THE SAME TIME WAITS HERE AND THEN FINDS IT ALREADY CLOSED
                await _dbContext.Database.ExecuteSqlAsync($"SELECT id FROM freezes WHERE id = {id} FOR UPDATE");

                var freeze = await _dbContext.Freezes.FirstOrDefaultAsync(f => f.Id == id);
                if (freeze == null)
                    return NotFound(new { message = $"The freeze with id {id} was not found" });

                if (freeze.EndDate != null)
                    return Conflict(new { message = $"The freeze with id {id} was already closed" });

                if (closeDTO.EndDate < freeze.StartDate)
                    return BadRequest(new { message = "The freeze end date cannot be earlier than its start date" });

                // A TRACKED CHANGE AND NOT A BULK UPDATE, SO THE AUDIT INTERCEPTOR SEES IT
                freeze.EndDate = closeDTO.EndDate;
                freeze.UpdatedBy = _currentUserService.UserId;
                freeze.UpdatedDate = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LOAN FREEZE CLOSE ERROR");
                return StatusCode(500, new { message = "ERROR COULD NOT CLOSE THE LOAN FREEZE" });
            }
        }

        [HttpGet("loans/{loanId:guid}/freezes")]
        public async Task<ActionResult<IEnumerable<FreezeDTO>>> GetFreezes([FromRoute] Guid loanId)
        {
            try
            {
                if (!await _dbContext.Loans.AnyAsync(l => l.Id == loanId))
                    return NotFound(new { message = $"The loan with id {loanId} was not found" });

                var freezes = await _dbContext.Freezes
                    .Where(f => f.LoanId == loanId)
                    .OrderByDescending(f => f.StartDate)
                    .ThenByDescending(f => f.CreatedDate)
                    .Select(f => new FreezeDTO
                    {
                        Id = f.Id,
                        LoanId = f.LoanId,
                        StartDate = f.StartDate,
                        EndDate = f.EndDate,
                        Reason = f.Reason,
                        AuthorizedBy = f.AuthorizedBy,
                        CreatedDate = f.CreatedDate,
                    })
                    .ToListAsync();

                return Ok(freezes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR FINDING LOAN FREEZES");
                return StatusCode(500, new { message = "ERROR FINDING LOAN FREEZES" });
            }
        }
    }
}
