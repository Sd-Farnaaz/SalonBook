using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalonBook.Api.Common;
using SalonBook.Api.Data;
using SalonBook.Api.DTOs;
using SalonBook.Api.Models;
using SalonBook.Api.Services;

namespace SalonBook.Api.Controllers;

[ApiController]
[Route("api/admin/automation")]
[Authorize(Policy = "SalonAdminOnly")]
public class AdminAutomationController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly SalonBook.Api.Configurations.EmailOptions _emailOptions;
    private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;
    private readonly ILogger<AdminAutomationController> _logger;

    public AdminAutomationController(
        ApplicationDbContext dbContext,
        ICurrentUserService currentUserService,
        Microsoft.Extensions.Options.IOptions<SalonBook.Api.Configurations.EmailOptions> emailOptions,
        Microsoft.AspNetCore.Hosting.IWebHostEnvironment env,
        ILogger<AdminAutomationController> logger)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _emailOptions = emailOptions?.Value ?? new SalonBook.Api.Configurations.EmailOptions();
        _env = env;
        _logger = logger;
    }

    /// <summary>
    private string GetWebAppBaseUrl()
    {
        var configuredUrl = _emailOptions.WebAppBaseUrl?.Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(configuredUrl) &&
            !configuredUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase) &&
            !configuredUrl.Contains("127.0.0.1"))
        {
            return configuredUrl;
        }

        if (_env.IsDevelopment())
        {
            return string.IsNullOrWhiteSpace(configuredUrl) ? "http://localhost:5173" : configuredUrl;
        }

        return "https://salonbook-frontend.onrender.com";
    }

    private static string Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) ? string.Empty : (value.Length <= maxLength ? value : value[..maxLength]);

    /// Live Automated WhatsApp, SMS, and Email delivery log feed.
    /// </summary>
    [HttpGet("reminders/logs")]
    [ProducesResponseType(typeof(List<AutomatedMessageLogResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMessageLogs([FromQuery] int limit = 50, CancellationToken cancellationToken = default)
    {
        var salonId = _currentUserService.SalonId;
        if (!salonId.HasValue) return Forbid();

        limit = Math.Clamp(limit, 1, 100);

        var notifications = await _dbContext.Notifications
            .AsNoTracking()
            .Include(n => n.Recipient)
            .Where(n => n.SalonId == salonId.Value)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(limit)
            .ToListAsync(cancellationToken);

        var logs = notifications.Select(n => new AutomatedMessageLogResponse
        {
            Id = n.Id,
            RecipientUserId = n.RecipientUserId,
            RecipientName = n.Recipient?.FullName ?? "Valued Client",
            RecipientContact = n.Recipient?.Email ?? "",
            AppointmentId = n.AppointmentId,
            Type = n.Type,
            Channel = n.Channel,
            Subject = n.Subject,
            Message = n.Message,
            Status = n.Status,
            SentAtUtc = n.SentAtUtc,
            CreatedAtUtc = n.CreatedAtUtc
        }).ToList();

        return Ok(logs);
    }

    /// <summary>
    /// Identify past clients who haven't booked in >= 6 weeks (42 days) for retention campaigns.
    /// </summary>
    [HttpGet("reengagement/candidates")]
    [ProducesResponseType(typeof(List<ReEngagementCandidateResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReEngagementCandidates([FromQuery] int minWeeks = 6, CancellationToken cancellationToken = default)
    {
        var salonId = _currentUserService.SalonId;
        if (!salonId.HasValue) return Forbid();

        var thresholdDays = Math.Max(14, minWeeks * 7);
        var cutoffDate = DateTime.UtcNow.AddDays(-thresholdDays);
        var now = DateTime.UtcNow;

        // Find customer IDs with completed appointments at this salon
        var completedAppointments = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.Customer)
            .Where(a => a.SalonId == salonId.Value && a.Status == AppointmentStatuses.Completed)
            .ToListAsync(cancellationToken);

        // Group by customer
        var grouped = completedAppointments
            .GroupBy(a => a.CustomerId)
            .ToList();

        // Check for upcoming bookings
        var customerIdsWithUpcoming = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.SalonId == salonId.Value &&
                        a.StartAtUtc >= now &&
                        (a.Status == AppointmentStatuses.Confirmed || a.Status == AppointmentStatuses.Pending))
            .Select(a => a.CustomerId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var upcomingSet = new HashSet<int>(customerIdsWithUpcoming);
        var candidates = new List<ReEngagementCandidateResponse>();

        foreach (var group in grouped)
        {
            var customerId = group.Key;
            if (upcomingSet.Contains(customerId)) continue; // Already booked upcoming visit

            var latestVisit = group.OrderByDescending(a => a.StartAtUtc).First();
            if (latestVisit.StartAtUtc <= cutoffDate)
            {
                var daysSince = (int)(now - latestVisit.StartAtUtc).TotalDays;
                var customer = latestVisit.Customer;

                candidates.Add(new ReEngagementCandidateResponse
                {
                    CustomerId = customerId,
                    CustomerName = customer?.FullName ?? "Valued Client",
                    CustomerEmail = customer?.Email ?? "",
                    CustomerPhone = null,
                    LastVisitDate = latestVisit.StartAtUtc,
                    DaysSinceLastVisit = daysSince,
                    WeeksSinceLastVisit = daysSince / 7,
                    TotalPastVisits = group.Count(),
                    LastServiceName = latestVisit.ServiceNameAtBooking
                });
            }
        }

        return Ok(candidates.OrderByDescending(c => c.DaysSinceLastVisit).ToList());
    }

    /// <summary>
    /// Broadcast an automated "We Miss You" 10% loyalty campaign to selected lapsed clients.
    /// </summary>
    [HttpPost("reengagement/dispatch")]
    [ProducesResponseType(typeof(ReEngagementResultResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> DispatchReEngagementCampaign([FromBody] SendReEngagementRequest request, CancellationToken cancellationToken)
    {
        var salonId = _currentUserService.SalonId;
        if (!salonId.HasValue) return Forbid();

        var salon = await _dbContext.Salons.FirstOrDefaultAsync(s => s.Id == salonId.Value, cancellationToken);
        if (salon == null) return NotFound("Salon not found.");

        var targetCustomerIds = request.CustomerIds?.Distinct().ToList() ?? new List<int>();
        if (targetCustomerIds.Count == 0)
        {
            return BadRequest(new { message = "At least one customer must be selected." });
        }

        var customers = await _dbContext.Users
            .Where(u => targetCustomerIds.Contains(u.Id))
            .ToListAsync(cancellationToken);

        var coupon = string.IsNullOrWhiteSpace(request.CouponCode) ? "MISSYOU10" : request.CouponCode.Trim().ToUpperInvariant();
        var discPct = request.DiscountPercentage > 0 ? request.DiscountPercentage : 10.00m;
        var channel = string.IsNullOrWhiteSpace(request.Channel) ? "WhatsApp" : request.Channel;

        int sentCount = 0;

        foreach (var customer in customers)
        {
            var defaultMsg = $"❤️ We miss you at {salon.Name}, {customer.FullName}! It's been a while since your last visit. Treat yourself with {discPct:0}% off your next booking using code '{coupon}'.\n✨ Book online: {GetWebAppBaseUrl()}/salons/{salon.Slug}\nSee you soon!";
            var msgText = string.IsNullOrWhiteSpace(request.CustomMessage) ? defaultMsg : request.CustomMessage.Replace("{CustomerName}", customer.FullName).Replace("{SalonName}", salon.Name).Replace("{Coupon}", coupon);

            var notification = new Notification
            {
                RecipientUserId = customer.Id,
                SalonId = salonId.Value,
                Type = "ReEngagement6Weeks",
                Channel = channel,
                Subject = Truncate($"❤️ We Miss You at {salon.Name}! Here's {discPct:0}% Off", 200),
                Message = Truncate(msgText, 2000),
                Status = "Sent",
                SentAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow
            };

            _dbContext.Notifications.Add(notification);
            sentCount++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new ReEngagementResultResponse
        {
            TotalTargeted = targetCustomerIds.Count,
            SuccessfullySent = sentCount,
            CouponCode = coupon,
            DiscountPercentage = discPct,
            DispatchedAtUtc = DateTime.UtcNow
        });
    }

    /// <summary>
    /// List all Off-Peak Dynamic Pricing / Flash Discount rules for the salon.
    /// </summary>
    [HttpGet("discounts")]
    [ProducesResponseType(typeof(List<OffPeakRuleDTO>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOffPeakRules(CancellationToken cancellationToken)
    {
        var salonId = _currentUserService.SalonId;
        if (!salonId.HasValue) return Forbid();

        var rules = await _dbContext.SalonOffPeakRules
            .AsNoTracking()
            .Where(r => r.SalonId == salonId.Value)
            .OrderBy(r => r.StartTime)
            .Select(r => new OffPeakRuleDTO
            {
                Id = r.Id,
                SalonId = r.SalonId,
                Name = r.Name,
                DaysOfWeek = r.DaysOfWeek,
                StartTime = r.StartTime,
                EndTime = r.EndTime,
                DiscountPercentage = r.DiscountPercentage,
                IsActive = r.IsActive,
                CreatedAtUtc = r.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(rules);
    }

    /// <summary>
    /// Create a new Off-Peak / Flash Discount rule (e.g. Tuesday & Wednesday 10 AM - 1 PM, 20% off).
    /// </summary>
    [HttpPost("discounts")]
    [ProducesResponseType(typeof(OffPeakRuleDTO), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateOffPeakRule([FromBody] CreateOffPeakRuleRequest request, CancellationToken cancellationToken)
    {
        var salonId = _currentUserService.SalonId;
        if (!salonId.HasValue) return Forbid();

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Rule name is required." });
        }

        if (string.IsNullOrWhiteSpace(request.DaysOfWeek))
        {
            return BadRequest(new { message = "At least one day of week is required." });
        }

        if (request.DiscountPercentage <= 0 || request.DiscountPercentage > 90)
        {
            return BadRequest(new { message = "Discount percentage must be between 1% and 90%." });
        }

        var rule = new SalonOffPeakRule
        {
            SalonId = salonId.Value,
            Name = request.Name.Trim(),
            DaysOfWeek = request.DaysOfWeek.Trim(),
            StartTime = string.IsNullOrWhiteSpace(request.StartTime) ? "10:00" : request.StartTime.Trim(),
            EndTime = string.IsNullOrWhiteSpace(request.EndTime) ? "13:00" : request.EndTime.Trim(),
            DiscountPercentage = request.DiscountPercentage,
            IsActive = request.IsActive,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.SalonOffPeakRules.Add(rule);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var dto = new OffPeakRuleDTO
        {
            Id = rule.Id,
            SalonId = rule.SalonId,
            Name = rule.Name,
            DaysOfWeek = rule.DaysOfWeek,
            StartTime = rule.StartTime,
            EndTime = rule.EndTime,
            DiscountPercentage = rule.DiscountPercentage,
            IsActive = rule.IsActive,
            CreatedAtUtc = rule.CreatedAtUtc
        };

        return CreatedAtAction(nameof(GetOffPeakRules), new { id = rule.Id }, dto);
    }

    /// <summary>
    /// Update an existing Off-Peak rule.
    /// </summary>
    [HttpPut("discounts/{id}")]
    [ProducesResponseType(typeof(OffPeakRuleDTO), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateOffPeakRule(int id, [FromBody] UpdateOffPeakRuleRequest request, CancellationToken cancellationToken)
    {
        var salonId = _currentUserService.SalonId;
        if (!salonId.HasValue) return Forbid();

        var rule = await _dbContext.SalonOffPeakRules
            .FirstOrDefaultAsync(r => r.Id == id && r.SalonId == salonId.Value, cancellationToken);

        if (rule == null)
        {
            return NotFound(new { message = "Off-peak rule not found." });
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(new { message = "Rule name is required." });
        }

        rule.Name = request.Name.Trim();
        rule.DaysOfWeek = request.DaysOfWeek.Trim();
        rule.StartTime = request.StartTime.Trim();
        rule.EndTime = request.EndTime.Trim();
        rule.DiscountPercentage = request.DiscountPercentage;
        rule.IsActive = request.IsActive;
        rule.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new OffPeakRuleDTO
        {
            Id = rule.Id,
            SalonId = rule.SalonId,
            Name = rule.Name,
            DaysOfWeek = rule.DaysOfWeek,
            StartTime = rule.StartTime,
            EndTime = rule.EndTime,
            DiscountPercentage = rule.DiscountPercentage,
            IsActive = rule.IsActive,
            CreatedAtUtc = rule.CreatedAtUtc
        });
    }

    /// <summary>
    /// Delete an Off-Peak rule.
    /// </summary>
    [HttpDelete("discounts/{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteOffPeakRule(int id, CancellationToken cancellationToken)
    {
        var salonId = _currentUserService.SalonId;
        if (!salonId.HasValue) return Forbid();

        var rule = await _dbContext.SalonOffPeakRules
            .FirstOrDefaultAsync(r => r.Id == id && r.SalonId == salonId.Value, cancellationToken);

        if (rule == null)
        {
            return NotFound(new { message = "Off-peak rule not found." });
        }

        _dbContext.SalonOffPeakRules.Remove(rule);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }
}
