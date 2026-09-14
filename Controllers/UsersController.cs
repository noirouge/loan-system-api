using LoanSystemAPI.Data;
using LoanSystemAPI.DTOs;
using LoanSystemAPI.Entities;
using LoanSystemAPI.Enums;
using LoanSystemAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LoanSystemAPI.Controllers
{

    [ApiController]
    [Route("api/users")]
    [Authorize(Roles = nameof(UserRole.ADMIN))]
    public class UsersController : Controller
    {
        public const int MinimumPasswordLength = 8;
        public const int MaximumTextLength = 50;

        private readonly ILogger<UsersController> _logger;
        private readonly AppDbContext _dbContext;
        private readonly ICurrentUserService _currentUserService;
        private readonly TimeProvider _timeProvider;
        private readonly PasswordHasher<User> _passwordHasher = new();

        public UsersController(ILogger<UsersController> logger, AppDbContext dbContext, ICurrentUserService currentUserService, TimeProvider timeProvider)
        {
            _logger = logger;
            _dbContext = dbContext;
            _currentUserService = currentUserService;
            _timeProvider = timeProvider;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<UserDTO>>> GetUsers()
        {
            try
            {
                var users = await _dbContext.Users
                    .Where(u => u.Status != UserStatus.DELETED)
                    .OrderBy(u => u.Username)
                    .Select(u => new UserDTO
                    {
                        Id = u.Id,
                        Name = u.Name,
                        Lastname = u.Lastname,
                        Username = u.Username,
                        Role = u.Role,
                        Status = u.Status,
                    })
                    .ToListAsync();

                return Ok(users);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CONSULTING USERS");
                return StatusCode(500, new { message = "ERROR CONSULTING USERS" });
            }
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<UserDTO>> GetUser([FromRoute] Guid id)
        {
            try
            {
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id && u.Status != UserStatus.DELETED);
                if (user == null)
                    return NotFound(new { message = $"The user with id {id} was not found" });

                return Ok(ToDTO(user));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR FINDING USER");
                return StatusCode(500, new { message = "ERROR FINDING USER" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> PostUser([FromBody] UserCreateDTO userDTO)
        {
            var error = ValidateNames(userDTO.Name, userDTO.Lastname)
                ?? ValidateText(userDTO.Username, "username")
                ?? ValidateRole(userDTO.Role)
                ?? ValidatePassword(userDTO.Password);
            if (error != null)
                return BadRequest(new { message = error });

            var user = new User
            {
                Id = Guid.NewGuid(),
                Name = userDTO.Name.Trim(),
                Lastname = userDTO.Lastname.Trim(),
                Username = userDTO.Username.Trim(),
                PasswordHash = "",
                Role = userDTO.Role,
                Status = UserStatus.ACTIVE,
                CreatedBy = _currentUserService.UserId,
                CreatedDate = DateTime.UtcNow,
            };
            user.PasswordHash = _passwordHasher.HashPassword(user, userDTO.Password);

            try
            {
                await _dbContext.Users.AddAsync(user);
                try
                {
                    await _dbContext.SaveChangesAsync();
                }
                // THE USERNAME IS UNIQUE AMONG ALL THE USERS, THE DELETED ONES INCLUDED
                catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "uq_users_username" })
                {
                    return Conflict(new { message = $"The username {user.Username} is already taken" });
                }

                return CreatedAtAction(nameof(GetUser), new { id = user.Id }, ToDTO(user));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR CREATING USER");
                return StatusCode(500, new { message = "ERROR COULD NOT CREATE THE USER" });
            }
        }

        [HttpPut]
        public async Task<ActionResult<UserDTO>> PutUser([FromBody] UserUpdateDTO userDTO)
        {
            var changesPassword = !string.IsNullOrEmpty(userDTO.Password);
            var error = ValidateNames(userDTO.Name, userDTO.Lastname)
                ?? ValidateRole(userDTO.Role)
                ?? (userDTO.Status != UserStatus.ACTIVE && userDTO.Status != UserStatus.INACTIVE ? "The status can only be ACTIVE or INACTIVE. A user is deleted with DELETE" : null)
                ?? (changesPassword ? ValidatePassword(userDTO.Password!) : null);
            if (error != null)
                return BadRequest(new { message = error });

            try
            {
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == userDTO.Id && u.Status != UserStatus.DELETED);
                if (user == null)
                    return NotFound(new { message = $"The user to update with id {userDTO.Id} was not found" });

                // AN ADMIN CANNOT LOCK THEMSELVES OUT: ANOTHER ADMIN CHANGES THEIR ROLE OR STATUS (D-064)
                if (user.Id == _currentUserService.UserId && (userDTO.Role != user.Role || userDTO.Status != user.Status))
                    return BadRequest(new { message = "You cannot change your own role or status" });

                var closesSessions = changesPassword || userDTO.Role != user.Role || userDTO.Status != user.Status;

                user.Name = userDTO.Name.Trim();
                user.Lastname = userDTO.Lastname.Trim();
                user.Role = userDTO.Role;
                user.Status = userDTO.Status;
                if (changesPassword)
                    user.PasswordHash = _passwordHasher.HashPassword(user, userDTO.Password!);
                user.UpdatedBy = _currentUserService.UserId;
                user.UpdatedDate = DateTime.UtcNow;

                await using var transaction = await _dbContext.Database.BeginTransactionAsync();
                await _dbContext.SaveChangesAsync();
                if (closesSessions)
                    await RevokeSessionsAsync(user.Id);
                await transaction.CommitAsync();

                return Ok(ToDTO(user));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR UPDATING USER");
                return StatusCode(500, new { message = "ERROR COULD NOT UPDATE THE USER" });
            }
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeleteUser([FromRoute] Guid id)
        {
            if (id == _currentUserService.UserId)
                return BadRequest(new { message = "You cannot delete your own user" });

            try
            {
                var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id && u.Status != UserStatus.DELETED);
                if (user == null)
                    return NotFound(new { message = $"The user to delete with id {id} was not found" });

                user.Status = UserStatus.DELETED;
                user.UpdatedBy = _currentUserService.UserId;
                user.UpdatedDate = DateTime.UtcNow;

                await using var transaction = await _dbContext.Database.BeginTransactionAsync();
                await _dbContext.SaveChangesAsync();
                await RevokeSessionsAsync(user.Id);
                await transaction.CommitAsync();

                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR DELETING USER");
                return StatusCode(500, new { message = "ERROR COULD NOT DELETE THE USER" });
            }
        }

        // WITH ANOTHER ROLE, STATUS OR PASSWORD THE USER MUST LOG IN AGAIN. THE ACCESS TOKEN THEY HAVE STILL WORKS UNTIL IT EXPIRES
        private async Task RevokeSessionsAsync(Guid userId)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            await _dbContext.RefreshTokens
                .Where(t => t.UserId == userId && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, (DateTime?)now));
        }

        private static string? ValidateNames(string name, string lastname)
        {
            return ValidateText(name, "name") ?? ValidateText(lastname, "lastname");
        }

        private static string? ValidateText(string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > MaximumTextLength)
                return $"The {field} is required and can have at most {MaximumTextLength} characters";

            return null;
        }

        private static string? ValidateRole(UserRole role)
        {
            return Enum.IsDefined(role) ? null : "The role must be WORKER (1) or ADMIN (2)";
        }

        private static string? ValidatePassword(string password)
        {
            return password.Length >= MinimumPasswordLength ? null : $"The password must have at least {MinimumPasswordLength} characters";
        }

        private static UserDTO ToDTO(User user)
        {
            return new UserDTO
            {
                Id = user.Id,
                Name = user.Name,
                Lastname = user.Lastname,
                Username = user.Username,
                Role = user.Role,
                Status = user.Status,
            };
        }
    }
}
