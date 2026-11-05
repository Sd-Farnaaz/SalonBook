using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SalonBook.Api.Common;
using SalonBook.Api.Data;
using SalonBook.Api.DTOs;
using SalonBook.Api.Models;
using SalonBook.Api.Services;

namespace SalonBook.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly ISlugService _slugService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHostEnvironment _env;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateBuilder _emailTemplateBuilder;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        ApplicationDbContext dbContext,
        ITokenService tokenService,
        IPasswordHasher<User> passwordHasher,
        ISlugService slugService,
        ICurrentUserService currentUserService,
        IHostEnvironment env,
        IEmailService emailService,
        IEmailTemplateBuilder emailTemplateBuilder,
        ILogger<AuthController> logger)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _passwordHasher = passwordHasher;
        _slugService = slugService;
        _currentUserService = currentUserService;
        _env = env;
        _emailService = emailService;
        _emailTemplateBuilder = emailTemplateBuilder;
        _logger = logger;
    }

    #region SuperAdmin Endpoints

    /// <summary>
    /// Authenticate SuperAdmin platform owner.
    /// Rejects non-SuperAdmin accounts with 401 to prevent role enumeration.
    /// </summary>
    [HttpPost("superadmin/login")]
    [EnableRateLimiting("AuthRateLimit")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SuperAdminLogin([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        return await ProcessLoginAsync(request, Roles.SuperAdmin, cancellationToken);
    }

    [HttpPost("superadmin/refresh")]
    [EnableRateLimiting("AuthRateLimit")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SuperAdminRefresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        return await ProcessRefreshAsync(request.RefreshToken, Roles.SuperAdmin, cancellationToken);
    }

    [HttpPost("superadmin/logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> SuperAdminLogout([FromBody] RevokeTokenRequest request, CancellationToken cancellationToken)
    {
        return await ProcessLogoutAsync(request.RefreshToken, cancellationToken);
    }

    #endregion

    #region Salon Admin Endpoints

    /// <summary>
    /// Register a new Salon and its initial Administrator account transactionally.
    /// </summary>
    [HttpPost("admin/register")]
    [EnableRateLimiting("AuthRateLimit")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterAdmin([FromBody] RegisterAdminRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        // 1. Check email uniqueness
        var emailExists = await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (emailExists)
        {
            return Conflict(new { message = "An account with this email address already exists." });
        }

        // 2. Generate and ensure unique slug for Salon
        var baseSlug = _slugService.GenerateSlug(request.SalonName);
        var uniqueSlug = baseSlug;
        var counter = 1;

        while (await _dbContext.Salons.AnyAsync(s => s.Slug == uniqueSlug, cancellationToken))
        {
            uniqueSlug = $"{baseSlug}-{counter++}";
        }

        // 3. Transactional creation of Salon + Admin User using execution strategy for SQL Server resiliency
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var salon = new Salon
                {
                    Name = request.SalonName.Trim(),
                    Slug = uniqueSlug,
                    BusinessType = request.BusinessType.Trim(),
                    TimeZone = string.IsNullOrWhiteSpace(request.TimeZone) ? "Asia/Kolkata" : request.TimeZone.Trim(),
                    CurrencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode) ? "INR" : request.CurrencyCode.Trim().ToUpperInvariant(),
                    OnboardingState = "Created",
                    SubscriptionTier = "Starter",
                    SubscriptionBillingCycle = "Monthly",
                    SubscriptionStatus = "Trial",
                    SubscriptionPrice = 0.00m,
                    SubscriptionStartedAtUtc = DateTime.UtcNow,
                    SubscriptionRenewsAtUtc = DateTime.UtcNow.AddDays(30),
                    TrialEndsAtUtc = DateTime.UtcNow.AddDays(30),
                    HasUsedTrial = true,
                    CreatedAtUtc = DateTime.UtcNow
                };

                _dbContext.Salons.Add(salon);
                await _dbContext.SaveChangesAsync(cancellationToken);

                var adminUser = new User
                {
                    SalonId = salon.Id,
                    FullName = request.FullName.Trim(),
                    Email = normalizedEmail,
                    Role = Roles.Admin, // Hardcoded server-side; client cannot choose role
                    IsActive = true,
                    IsEmailVerified = false,
                    CreatedAtUtc = DateTime.UtcNow
                };

                adminUser.PasswordHash = _passwordHasher.HashPassword(adminUser, request.Password);
                _dbContext.Users.Add(adminUser);
                await _dbContext.SaveChangesAsync(cancellationToken);

                // Generate Tokens
                var (accessToken, accessExpiresAt, jwtId) = _tokenService.GenerateAccessToken(adminUser, salon.Slug);
                var (rawRefreshToken, refreshTokenHash, refreshExpiresAt, familyId) = _tokenService.GenerateRefreshToken();

                var refreshTokenEntity = new RefreshToken
                {
                    UserId = adminUser.Id,
                    TokenHash = refreshTokenHash,
                    JwtId = jwtId,
                    FamilyId = familyId,
                    CreatedAtUtc = DateTime.UtcNow,
                    ExpiresAtUtc = refreshExpiresAt,
                    CreatedByIp = HttpContext.Connection.RemoteIpAddress?.ToString()
                };

                _dbContext.RefreshTokens.Add(refreshTokenEntity);
                await _dbContext.SaveChangesAsync(cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("Salon '{SalonName}' ({Slug}) registered with Admin {Email}", salon.Name, salon.Slug, adminUser.Email);

                try
                {
                    var welcomeTemplate = _emailTemplateBuilder.BuildSalonAdminWelcomeEmail(adminUser.FullName, salon.Name, salon.Slug);
                    await _emailService.SendEmailAsync(adminUser.Email, welcomeTemplate.Subject, welcomeTemplate.HtmlBody, welcomeTemplate.PlainTextBody, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to dispatch partner welcome email for {Email}", adminUser.Email);
                }

                var response = new AuthResponse
                {
                    AccessToken = accessToken,
                    RefreshToken = rawRefreshToken,
                    ExpiresInSeconds = (int)(accessExpiresAt - DateTime.UtcNow).TotalSeconds,
                    User = MapUserProfile(adminUser, salon)
                };

                return StatusCode(StatusCodes.Status201Created, response);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                _logger.LogError(ex, "Failed to register salon and admin account for {Email}", normalizedEmail);
                throw;
            }
        });
    }

    [HttpPost("admin/login")]
    [EnableRateLimiting("AuthRateLimit")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AdminLogin([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        return await ProcessLoginAsync(request, Roles.Admin, cancellationToken);
    }

    [HttpPost("admin/refresh")]
    [EnableRateLimiting("AuthRateLimit")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> AdminRefresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        return await ProcessRefreshAsync(request.RefreshToken, Roles.Admin, cancellationToken);
    }

    [HttpPost("admin/logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AdminLogout([FromBody] RevokeTokenRequest request, CancellationToken cancellationToken)
    {
        return await ProcessLogoutAsync(request.RefreshToken, cancellationToken);
    }

    #endregion

    #region Customer (User) Endpoints

    /// <summary>
    /// Register a new Global Customer Account.
    /// </summary>
    [HttpPost("user/register")]
    [EnableRateLimiting("AuthRateLimit")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RegisterCustomer([FromBody] RegisterUserRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var emailExists = await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken);
        if (emailExists)
        {
            return Conflict(new { message = "An account with this email address already exists." });
        }

        var customer = new User
        {
            FullName = request.FullName.Trim(),
            Email = normalizedEmail,
            Role = Roles.User, // Enforced server-side
            SalonId = null,    // Customer accounts are global across all salons
            IsActive = true,
            IsEmailVerified = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        customer.PasswordHash = _passwordHasher.HashPassword(customer, request.Password);
        _dbContext.Users.Add(customer);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var (accessToken, accessExpiresAt, jwtId) = _tokenService.GenerateAccessToken(customer);
        var (rawRefreshToken, refreshTokenHash, refreshExpiresAt, familyId) = _tokenService.GenerateRefreshToken();

        var refreshTokenEntity = new RefreshToken
        {
            UserId = customer.Id,
            TokenHash = refreshTokenHash,
            JwtId = jwtId,
            FamilyId = familyId,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = refreshExpiresAt,
            CreatedByIp = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        _dbContext.RefreshTokens.Add(refreshTokenEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("New customer registered: {Email}", customer.Email);

        try
        {
            var welcomeTemplate = _emailTemplateBuilder.BuildCustomerWelcomeEmail(customer.FullName, customer.Email);
            await _emailService.SendEmailAsync(customer.Email, welcomeTemplate.Subject, welcomeTemplate.HtmlBody, welcomeTemplate.PlainTextBody, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to dispatch customer welcome email for {Email}", customer.Email);
        }

        var response = new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            ExpiresInSeconds = (int)(accessExpiresAt - DateTime.UtcNow).TotalSeconds,
            User = MapUserProfile(customer, null)
        };

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("user/login")]
    [EnableRateLimiting("AuthRateLimit")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CustomerLogin([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        return await ProcessLoginAsync(request, Roles.User, cancellationToken);
    }

    [HttpPost("user/refresh")]
    [EnableRateLimiting("AuthRateLimit")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CustomerRefresh([FromBody] RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        return await ProcessRefreshAsync(request.RefreshToken, Roles.User, cancellationToken);
    }

    [HttpPost("user/logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CustomerLogout([FromBody] RevokeTokenRequest request, CancellationToken cancellationToken)
    {
        return await ProcessLogoutAsync(request.RefreshToken, cancellationToken);
    }

    #endregion

    #region Shared Endpoints

    /// <summary>
    /// Retrieve authenticated user profile with safe tenant details.
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var user = await _dbContext.Users
            .Include(u => u.Salon)
            .FirstOrDefaultAsync(u => u.Id == userId.Value, cancellationToken);

        if (user == null || !user.IsActive)
        {
            return Unauthorized(new { message = "User account not found or disabled." });
        }

        return Ok(MapUserProfile(user, user.Salon));
    }

    /// <summary>
    /// Change password for authenticated user and revoke existing refresh tokens.
    /// </summary>
    [Authorize]
    [HttpPost("change-password")]
    [EnableRateLimiting("AuthRateLimit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (!userId.HasValue)
        {
            return Unauthorized();
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId.Value, cancellationToken);

        if (user == null || !user.IsActive)
        {
            return Unauthorized();
        }

        var verifyResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            return BadRequest(new { message = "Current password is incorrect." });
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        user.UpdatedAtUtc = DateTime.UtcNow;

        // Revoke all active refresh tokens to force re-login on all devices
        var activeTokens = await _dbContext.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.RevokedAtUtc == null && rt.ExpiresAtUtc > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAtUtc = DateTime.UtcNow;
            token.ReasonRevoked = "Password Changed";
            token.RevokedByIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password changed successfully for User ID {UserId}", user.Id);
        return Ok(new { message = "Password changed successfully. Please log in again." });
    }

    /// <summary>
    /// Request password reset token.
    /// Always returns a generic response to prevent user enumeration.
    /// In Development, devResetToken is provided in response for automated testing.
    /// </summary>
    [HttpPost("forgot-password")]
    [EnableRateLimiting("AuthRateLimit")]
    [ProducesResponseType(typeof(ForgotPasswordResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        string? devToken = null;

        if (user != null && user.IsActive)
        {
            var (rawToken, tokenHash, expiresAtUtc) = _tokenService.GeneratePasswordResetToken();

            var resetTokenEntity = new PasswordResetToken
            {
                UserId = user.Id,
                TokenHash = tokenHash,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = expiresAtUtc,
                CreatedByIp = HttpContext.Connection.RemoteIpAddress?.ToString()
            };

            _dbContext.PasswordResetTokens.Add(resetTokenEntity);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Password reset token generated for user {Email}", normalizedEmail);

            // In Development mode, expose the token in the response for test verification
            if (_env.IsDevelopment() || _env.IsEnvironment("Testing"))
            {
                devToken = rawToken;
            }

            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                var resetTemplate = _emailTemplateBuilder.BuildPasswordResetEmail(user.FullName, rawToken, expirationMinutes: 30);
                await _emailService.SendEmailAsync(user.Email, resetTemplate.Subject, resetTemplate.HtmlBody, resetTemplate.PlainTextBody, cancellationToken);
            }
        }

        return Ok(new ForgotPasswordResponse
        {
            Message = "If your email is registered in our system, you will receive password reset instructions.",
            DevResetToken = devToken
        });
    }

    /// <summary>
    /// Reset password using valid reset token.
    /// </summary>
    [HttpPost("reset-password")]
    [EnableRateLimiting("AuthRateLimit")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        if (user == null || !user.IsActive)
        {
            return BadRequest(new { message = "Invalid email or reset token." });
        }

        var tokenHash = _tokenService.HashToken(request.ResetToken);
        var resetToken = await _dbContext.PasswordResetTokens
            .FirstOrDefaultAsync(prt => prt.UserId == user.Id && prt.TokenHash == tokenHash, cancellationToken);

        if (resetToken == null || resetToken.IsUsed || resetToken.IsExpired)
        {
            return BadRequest(new { message = "Invalid or expired password reset token." });
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, request.NewPassword);
        user.UpdatedAtUtc = DateTime.UtcNow;

        resetToken.UsedAtUtc = DateTime.UtcNow;
        resetToken.UsedByIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        // Revoke active refresh tokens
        var activeTokens = await _dbContext.RefreshTokens
            .Where(rt => rt.UserId == user.Id && rt.RevokedAtUtc == null && rt.ExpiresAtUtc > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAtUtc = DateTime.UtcNow;
            token.ReasonRevoked = "Password Reset";
            token.RevokedByIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password reset successfully for user {Email}", normalizedEmail);
        return Ok(new { message = "Password has been reset successfully. Please log in with your new password." });
    }

    #endregion

    #region Internal Helper Methods

    private async Task<IActionResult> ProcessLoginAsync(LoginRequest request, string expectedRole, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _dbContext.Users
            .Include(u => u.Salon)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, cancellationToken);

        // Generic error message to prevent account enumeration
        if (user == null || !user.IsActive)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        // Verify password
        var passwordVerification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (passwordVerification == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        // Strict role validation: Ensure user role matches the dedicated endpoint
        if (!string.Equals(user.Role, expectedRole, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("User {Email} with role {Role} attempted login via {ExpectedRole} endpoint.", user.Email, user.Role, expectedRole);
            return Unauthorized(new { message = "Invalid email or password." });
        }

        // Update LastLoginAtUtc
        user.LastLoginAtUtc = DateTime.UtcNow;

        // Generate Tokens
        var (accessToken, accessExpiresAt, jwtId) = _tokenService.GenerateAccessToken(user, user.Salon?.Slug);
        var (rawRefreshToken, refreshTokenHash, refreshExpiresAt, familyId) = _tokenService.GenerateRefreshToken();

        var refreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            JwtId = jwtId,
            FamilyId = familyId,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = refreshExpiresAt,
            CreatedByIp = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        _dbContext.RefreshTokens.Add(refreshTokenEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("User {Email} ({Role}) logged in successfully.", user.Email, user.Role);

        var response = new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            ExpiresInSeconds = (int)(accessExpiresAt - DateTime.UtcNow).TotalSeconds,
            User = MapUserProfile(user, user.Salon)
        };

        return Ok(response);
    }

    private async Task<IActionResult> ProcessRefreshAsync(string rawRefreshToken, string expectedRole, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return Unauthorized(new { message = "Refresh token is required." });
        }

        var tokenHash = _tokenService.HashToken(rawRefreshToken);

        var existingToken = await _dbContext.RefreshTokens
            .Include(rt => rt.User)
                .ThenInclude(u => u.Salon)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (existingToken == null)
        {
            return Unauthorized(new { message = "Invalid refresh token." });
        }

        // Token Reuse Detection: If a revoked token is used, revoke the entire family
        if (existingToken.IsRevoked)
        {
            _logger.LogWarning("Token reuse detected for family {FamilyId}! Revoking all tokens in family for User ID {UserId}.",
                existingToken.FamilyId, existingToken.UserId);

            var familyTokens = await _dbContext.RefreshTokens
                .Where(rt => rt.FamilyId == existingToken.FamilyId && rt.RevokedAtUtc == null)
                .ToListAsync(cancellationToken);

            foreach (var token in familyTokens)
            {
                token.RevokedAtUtc = DateTime.UtcNow;
                token.ReasonRevoked = "Token Reuse Detected";
                token.RevokedByIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return Unauthorized(new { message = "Invalid refresh token. Token family revoked due to reuse." });
        }

        if (existingToken.IsExpired)
        {
            return Unauthorized(new { message = "Refresh token has expired. Please log in again." });
        }

        var user = existingToken.User;
        if (user == null || !user.IsActive)
        {
            return Unauthorized(new { message = "User account is disabled or does not exist." });
        }

        if (!string.Equals(user.Role, expectedRole, StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized(new { message = "Invalid token for this role endpoint." });
        }

        // Rotate token: Invalidate current token and generate new one in the same family
        var (newRawRefreshToken, newRefreshTokenHash, newRefreshExpiresAt, _) = _tokenService.GenerateRefreshToken();
        var (newAccessToken, newAccessExpiresAt, newJwtId) = _tokenService.GenerateAccessToken(user, user.Salon?.Slug);

        existingToken.RevokedAtUtc = DateTime.UtcNow;
        existingToken.ReplacedByTokenHash = newRefreshTokenHash;
        existingToken.ReasonRevoked = "Rotated";
        existingToken.RevokedByIp = HttpContext.Connection.RemoteIpAddress?.ToString();

        var newRefreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = newRefreshTokenHash,
            JwtId = newJwtId,
            FamilyId = existingToken.FamilyId, // Preserves token family
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = newRefreshExpiresAt,
            CreatedByIp = HttpContext.Connection.RemoteIpAddress?.ToString()
        };

        _dbContext.RefreshTokens.Add(newRefreshTokenEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Refresh token rotated for user {Email}", user.Email);

        var response = new AuthResponse
        {
            AccessToken = newAccessToken,
            RefreshToken = newRawRefreshToken,
            ExpiresInSeconds = (int)(newAccessExpiresAt - DateTime.UtcNow).TotalSeconds,
            User = MapUserProfile(user, user.Salon)
        };

        return Ok(response);
    }

    private async Task<IActionResult> ProcessLogoutAsync(string rawRefreshToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return Ok(new { message = "Logged out successfully." });
        }

        var tokenHash = _tokenService.HashToken(rawRefreshToken);
        var token = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        if (token != null && !token.IsRevoked)
        {
            token.RevokedAtUtc = DateTime.UtcNow;
            token.ReasonRevoked = "User Logout";
            token.RevokedByIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return Ok(new { message = "Logged out successfully." });
    }

    private static UserProfileResponse MapUserProfile(User user, Salon? salon)
    {
        return new UserProfileResponse
        {
            Id = user.Id,
            SalonId = user.SalonId,
            SalonSlug = salon?.Slug,
            SalonName = salon?.Name,
            FullName = user.FullName,
            Email = user.Email,
            Role = user.Role,
            IsEmailVerified = user.IsEmailVerified,
            CreatedAtUtc = user.CreatedAtUtc
        };
    }

    #endregion
}
