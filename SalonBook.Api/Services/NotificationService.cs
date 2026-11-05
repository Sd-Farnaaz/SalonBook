using Microsoft.EntityFrameworkCore;
using SalonBook.Api.Data;
using SalonBook.Api.DTOs;
using SalonBook.Api.Models;

namespace SalonBook.Api.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateBuilder _templateBuilder;
    private readonly SalonBook.Api.Configurations.EmailOptions _emailOptions;
    private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ApplicationDbContext dbContext,
        IEmailService emailService,
        IEmailTemplateBuilder templateBuilder,
        Microsoft.Extensions.Options.IOptions<SalonBook.Api.Configurations.EmailOptions> emailOptions,
        Microsoft.AspNetCore.Hosting.IWebHostEnvironment env,
        ILogger<NotificationService> logger)
    {
        _dbContext = dbContext;
        _emailService = emailService;
        _templateBuilder = templateBuilder;
        _emailOptions = emailOptions?.Value ?? new SalonBook.Api.Configurations.EmailOptions();
        _env = env;
        _logger = logger;
    }

    public static string Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) ? string.Empty : (value.Length <= maxLength ? value : value[..maxLength]);

    public static string? TruncateNullable(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) ? null : (value.Length <= maxLength ? value : value[..maxLength]);

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

    private static string TruncateMessage(string? message, int maxLength = 2000) =>
        Truncate(message, maxLength);




    public async Task DispatchAppointmentEventAsync(Appointment appointment, string eventType, CancellationToken cancellationToken = default)
    {
        // Load full appointment details with navigations if needed
        var appt = await _dbContext.Appointments
            .Include(a => a.Salon)
            .Include(a => a.Customer)
            .Include(a => a.Service)
            .Include(a => a.Staff)
            .FirstOrDefaultAsync(a => a.Id == appointment.Id, cancellationToken)
            ?? appointment;

        if (appt.Salon == null)
            appt.Salon = await _dbContext.Salons.FindAsync(new object[] { appt.SalonId }, cancellationToken) ?? new Salon { Name = "Salon" };
        if (appt.Customer == null)
            appt.Customer = await _dbContext.Users.FindAsync(new object[] { appt.CustomerId }, cancellationToken) ?? new User { FullName = "Customer", Email = "" };
        if (appt.Staff == null)
            appt.Staff = await _dbContext.Staff.FindAsync(new object[] { appt.StaffId }, cancellationToken) ?? new Staff { FullName = "Staff Member" };

        var salonAdmins = await _dbContext.Users
            .Where(u => u.SalonId == appt.SalonId && u.Role == "Admin" && u.IsActive)
            .ToListAsync(cancellationToken);

        switch (eventType)
        {
            case "AppointmentCreated":
                await HandleAppointmentCreatedAsync(appt, salonAdmins, cancellationToken);
                break;

            case "AppointmentCancelled":
                await HandleAppointmentCancelledAsync(appt, salonAdmins, cancellationToken);
                break;

            case "AppointmentRescheduled":
                await HandleAppointmentRescheduledAsync(appt, salonAdmins, cancellationToken);
                break;

            case "AppointmentConfirmed":
            case "AppointmentStatusChanged":
                await HandleAppointmentStatusChangedAsync(appt, salonAdmins, eventType, cancellationToken);
                break;

            case "PaymentProofSubmitted":
                await HandlePaymentProofSubmittedAsync(appt, salonAdmins, cancellationToken);
                break;

            case "PaymentVerified":
            case "PaymentRejected":
            case "PaymentRefunded":
                await HandlePaymentStatusEventAsync(appt, salonAdmins, eventType, cancellationToken);
                break;

            default:
                _logger.LogWarning("Unknown appointment notification event type: {EventType}", eventType);
                break;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task HandleAppointmentCreatedAsync(Appointment appt, List<User> salonAdmins, CancellationToken cancellationToken)
    {
        var formattedDate = appt.StartAtUtc.ToString("yyyy-MM-dd HH:mm");

        // 1. Customer InApp Notification
        var customerInApp = new Notification
        {
            RecipientUserId = appt.CustomerId,
            SalonId = appt.SalonId,
            AppointmentId = appt.Id,
            Type = "AppointmentCreated",
            Channel = "InApp",
            Subject = Truncate($"Appointment Confirmed: {appt.ServiceNameAtBooking}", 200),
            Message = Truncate($"Your appointment for {appt.ServiceNameAtBooking} with {appt.Staff.FullName} at {appt.Salon.Name} is confirmed for {formattedDate} UTC.", 2000),
            Status = "Sent",
            SentAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        };
        _dbContext.Notifications.Add(customerInApp);

        // 2. Customer Email Notification
        if (!string.IsNullOrWhiteSpace(appt.Customer.Email))
        {
            var template = _templateBuilder.BuildBookingConfirmationEmail(appt);


            var emailResult = await _emailService.SendEmailAsync(appt.Customer.Email, template.Subject, template.HtmlBody, template.PlainTextBody, cancellationToken);

            var customerEmail = new Notification
            {
                RecipientUserId = appt.CustomerId,
                SalonId = appt.SalonId,
                AppointmentId = appt.Id,
                Type = "AppointmentCreated",
                Channel = "Email",
                Subject = Truncate(template.Subject, 200),
                Message = TruncateMessage(template.PlainTextBody),
                Status = emailResult.Success ? "Sent" : (emailResult.Skipped ? "Skipped" : "Failed"),
                SentAtUtc = emailResult.Success ? DateTime.UtcNow : null,
                ErrorMessage = TruncateNullable(emailResult.ErrorMessage, 1000),
                LastErrorCode = TruncateNullable(emailResult.ErrorCode, 50),
                CreatedAtUtc = DateTime.UtcNow
            };
            _dbContext.Notifications.Add(customerEmail);
        }

        // 3. Salon Admins InApp & Email Notifications
        foreach (var admin in salonAdmins)
        {
            var adminInApp = new Notification
            {
                RecipientUserId = admin.Id,
                SalonId = appt.SalonId,
                AppointmentId = appt.Id,
                Type = "AppointmentCreated",
                Channel = "InApp",
                Subject = Truncate($"New Booking: {appt.Customer.FullName} - {appt.ServiceNameAtBooking}", 200),
                Message = $"{appt.Customer.FullName} booked {appt.ServiceNameAtBooking} with {appt.Staff.FullName} for {formattedDate} UTC.",
                Status = "Sent",
                SentAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow
            };
            _dbContext.Notifications.Add(adminInApp);

            if (!string.IsNullOrWhiteSpace(admin.Email))
            {
                var adminTemplate = _templateBuilder.BuildOwnerNewBookingEmail(appt, admin.FullName);


                var adminEmailResult = await _emailService.SendEmailAsync(admin.Email, adminTemplate.Subject, adminTemplate.HtmlBody, adminTemplate.PlainTextBody, cancellationToken);

                var adminEmail = new Notification
                {
                    RecipientUserId = admin.Id,
                    SalonId = appt.SalonId,
                    AppointmentId = appt.Id,
                    Type = "AppointmentCreated",
                    Channel = "Email",
                    Subject = Truncate(adminTemplate.Subject, 200),
                    Message = TruncateMessage(adminTemplate.PlainTextBody),
                    Status = adminEmailResult.Success ? "Sent" : (adminEmailResult.Skipped ? "Skipped" : "Failed"),
                    SentAtUtc = adminEmailResult.Success ? DateTime.UtcNow : null,
                    ErrorMessage = TruncateNullable(adminEmailResult.ErrorMessage, 1000),
                    LastErrorCode = TruncateNullable(adminEmailResult.ErrorCode, 50),
                    CreatedAtUtc = DateTime.UtcNow
                };
                _dbContext.Notifications.Add(adminEmail);
            }
        }

        // 4. Schedule Reminders (24H, 2H before, and 1H post-visit thank-you)
        var reminder24HTime = appt.StartAtUtc.AddHours(-24);
        if (reminder24HTime > DateTime.UtcNow)
        {
            _dbContext.AppointmentReminders.Add(new AppointmentReminder
            {
                AppointmentId = appt.Id,
                ReminderType = "24Hours",
                ScheduledForUtc = reminder24HTime,
                Status = "Pending",
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        var reminder2HTime = appt.StartAtUtc.AddHours(-2);
        if (reminder2HTime > DateTime.UtcNow)
        {
            _dbContext.AppointmentReminders.Add(new AppointmentReminder
            {
                AppointmentId = appt.Id,
                ReminderType = "2Hours",
                ScheduledForUtc = reminder2HTime,
                Status = "Pending",
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        var reminderPostVisitTime = appt.EndAtUtc.AddHours(1);
        _dbContext.AppointmentReminders.Add(new AppointmentReminder
        {
            AppointmentId = appt.Id,
            ReminderType = "1HourAfter",
            ScheduledForUtc = reminderPostVisitTime,
            Status = "Pending",
            CreatedAtUtc = DateTime.UtcNow
        });
    }

    private async Task HandleAppointmentCancelledAsync(Appointment appt, List<User> salonAdmins, CancellationToken cancellationToken)
    {
        var reason = string.IsNullOrWhiteSpace(appt.CancellationReason) ? "No reason specified" : appt.CancellationReason;

        // Customer InApp
        _dbContext.Notifications.Add(new Notification
        {
            RecipientUserId = appt.CustomerId,
            SalonId = appt.SalonId,
            AppointmentId = appt.Id,
            Type = "AppointmentCancelled",
            Channel = "InApp",
            Subject = Truncate($"Appointment Cancelled: {appt.ServiceNameAtBooking}", 200),
            Message = $"Your appointment for {appt.ServiceNameAtBooking} at {appt.Salon.Name} on {appt.StartAtUtc:yyyy-MM-dd HH:mm} UTC has been cancelled. Reason: {reason}",
            Status = "Sent",
            SentAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        });

        // Customer Email
        if (!string.IsNullOrWhiteSpace(appt.Customer.Email))
        {
            var cancelTemplate = _templateBuilder.BuildAppointmentCancelledEmail(appt, isForCustomer: true);
            var emailResult = await _emailService.SendEmailAsync(appt.Customer.Email, cancelTemplate.Subject, cancelTemplate.HtmlBody, cancelTemplate.PlainTextBody, cancellationToken);

            _dbContext.Notifications.Add(new Notification
            {
                RecipientUserId = appt.CustomerId,
                SalonId = appt.SalonId,
                AppointmentId = appt.Id,
                Type = "AppointmentCancelled",
                Channel = "Email",
                Subject = Truncate(cancelTemplate.Subject, 200),
                Message = TruncateMessage(cancelTemplate.PlainTextBody),
                Status = emailResult.Success ? "Sent" : (emailResult.Skipped ? "Skipped" : "Failed"),
                SentAtUtc = emailResult.Success ? DateTime.UtcNow : null,
                ErrorMessage = TruncateNullable(emailResult.ErrorMessage, 1000),
                LastErrorCode = TruncateNullable(emailResult.ErrorCode, 50),
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        // Admins InApp & Email
        foreach (var admin in salonAdmins)
        {
            _dbContext.Notifications.Add(new Notification
            {
                RecipientUserId = admin.Id,
                SalonId = appt.SalonId,
                AppointmentId = appt.Id,
                Type = "AppointmentCancelled",
                Channel = "InApp",
                Subject = Truncate($"Appointment Cancelled: {appt.Customer.FullName} - {appt.ServiceNameAtBooking}", 200),
                Message = $"Appointment #{appt.Id} for {appt.Customer.FullName} with {appt.Staff.FullName} was cancelled. Reason: {reason}",
                Status = "Sent",
                SentAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow
            });

            if (!string.IsNullOrWhiteSpace(admin.Email))
            {
                var adminCancelTemplate = _templateBuilder.BuildAppointmentCancelledEmail(appt, isForCustomer: false, adminName: admin.FullName);
                var adminEmailResult = await _emailService.SendEmailAsync(admin.Email, adminCancelTemplate.Subject, adminCancelTemplate.HtmlBody, adminCancelTemplate.PlainTextBody, cancellationToken);

                _dbContext.Notifications.Add(new Notification
                {
                    RecipientUserId = admin.Id,
                    SalonId = appt.SalonId,
                    AppointmentId = appt.Id,
                    Type = "AppointmentCancelled",
                    Channel = "Email",
                    Subject = Truncate(adminCancelTemplate.Subject, 200),
                    Message = TruncateMessage(adminCancelTemplate.PlainTextBody),
                    Status = adminEmailResult.Success ? "Sent" : (adminEmailResult.Skipped ? "Skipped" : "Failed"),
                    SentAtUtc = adminEmailResult.Success ? DateTime.UtcNow : null,
                    ErrorMessage = TruncateNullable(adminEmailResult.ErrorMessage, 1000),
                    LastErrorCode = TruncateNullable(adminEmailResult.ErrorCode, 50),
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        // Cancel / Skip all pending reminders for this appointment
        var pendingReminders = await _dbContext.AppointmentReminders
            .Where(r => r.AppointmentId == appt.Id && r.Status == "Pending")
            .ToListAsync(cancellationToken);

        foreach (var reminder in pendingReminders)
        {
            reminder.Status = "Skipped";
            reminder.LastError = "Appointment was cancelled";
            reminder.ProcessedAtUtc = DateTime.UtcNow;
        }
    }

    private async Task HandleAppointmentRescheduledAsync(Appointment appt, List<User> salonAdmins, CancellationToken cancellationToken)
    {
        var formattedDate = appt.StartAtUtc.ToString("yyyy-MM-dd HH:mm");

        // 1. Customer InApp Notification
        _dbContext.Notifications.Add(new Notification
        {
            RecipientUserId = appt.CustomerId,
            SalonId = appt.SalonId,
            AppointmentId = appt.Id,
            Type = "AppointmentRescheduled",
            Channel = "InApp",
            Subject = Truncate($"Appointment Rescheduled: {appt.ServiceNameAtBooking}", 200),
            Message = $"Your appointment for {appt.ServiceNameAtBooking} at {appt.Salon.Name} has been rescheduled to {formattedDate} UTC.",
            Status = "Sent",
            SentAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        });

        // 2. Customer Email
        if (!string.IsNullOrWhiteSpace(appt.Customer.Email))
        {
            var reschedTemplate = _templateBuilder.BuildAppointmentRescheduledEmail(appt, isForCustomer: true);
            var emailResult = await _emailService.SendEmailAsync(appt.Customer.Email, reschedTemplate.Subject, reschedTemplate.HtmlBody, reschedTemplate.PlainTextBody, cancellationToken);

            _dbContext.Notifications.Add(new Notification
            {
                RecipientUserId = appt.CustomerId,
                SalonId = appt.SalonId,
                AppointmentId = appt.Id,
                Type = "AppointmentRescheduled",
                Channel = "Email",
                Subject = Truncate(reschedTemplate.Subject, 200),
                Message = TruncateMessage(reschedTemplate.PlainTextBody),
                Status = emailResult.Success ? "Sent" : (emailResult.Skipped ? "Skipped" : "Failed"),
                SentAtUtc = emailResult.Success ? DateTime.UtcNow : null,
                ErrorMessage = TruncateNullable(emailResult.ErrorMessage, 1000),
                LastErrorCode = TruncateNullable(emailResult.ErrorCode, 50),
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        // 3. Admins InApp & Email Notifications
        foreach (var admin in salonAdmins)
        {
            _dbContext.Notifications.Add(new Notification
            {
                RecipientUserId = admin.Id,
                SalonId = appt.SalonId,
                AppointmentId = appt.Id,
                Type = "AppointmentRescheduled",
                Channel = "InApp",
                Subject = Truncate($"Appointment Rescheduled: {appt.Customer.FullName} - {appt.ServiceNameAtBooking}", 200),
                Message = $"Appointment #{appt.Id} for {appt.Customer.FullName} ({appt.ServiceNameAtBooking}) was rescheduled to {formattedDate} UTC.",
                Status = "Sent",
                SentAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow
            });

            if (!string.IsNullOrWhiteSpace(admin.Email))
            {
                var adminReschedTemplate = _templateBuilder.BuildAppointmentRescheduledEmail(appt, isForCustomer: false, adminName: admin.FullName);
                var adminEmailResult = await _emailService.SendEmailAsync(admin.Email, adminReschedTemplate.Subject, adminReschedTemplate.HtmlBody, adminReschedTemplate.PlainTextBody, cancellationToken);

                _dbContext.Notifications.Add(new Notification
                {
                    RecipientUserId = admin.Id,
                    SalonId = appt.SalonId,
                    AppointmentId = appt.Id,
                    Type = "AppointmentRescheduled",
                    Channel = "Email",
                    Subject = Truncate(adminReschedTemplate.Subject, 200),
                    Message = TruncateMessage(adminReschedTemplate.PlainTextBody),
                    Status = adminEmailResult.Success ? "Sent" : (adminEmailResult.Skipped ? "Skipped" : "Failed"),
                    SentAtUtc = adminEmailResult.Success ? DateTime.UtcNow : null,
                    ErrorMessage = TruncateNullable(adminEmailResult.ErrorMessage, 1000),
                    LastErrorCode = TruncateNullable(adminEmailResult.ErrorCode, 50),
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        // 4. Update pending reminders for new time
        var pendingReminders = await _dbContext.AppointmentReminders
            .Where(r => r.AppointmentId == appt.Id && r.Status == "Pending")
            .ToListAsync(cancellationToken);

        foreach (var reminder in pendingReminders)
        {
            if (reminder.ReminderType == "24Hours")
            {
                reminder.ScheduledForUtc = appt.StartAtUtc.AddHours(-24);
            }
            else if (reminder.ReminderType == "2Hours")
            {
                reminder.ScheduledForUtc = appt.StartAtUtc.AddHours(-2);
            }
            else if (reminder.ReminderType == "1HourAfter")
            {
                reminder.ScheduledForUtc = appt.EndAtUtc.AddHours(1);
            }
        }
    }


    private async Task HandleAppointmentStatusChangedAsync(Appointment appt, List<User> salonAdmins, string eventType, CancellationToken cancellationToken)
    {
        // Customer InApp
        _dbContext.Notifications.Add(new Notification
        {
            RecipientUserId = appt.CustomerId,
            SalonId = appt.SalonId,
            AppointmentId = appt.Id,
            Type = eventType,
            Channel = "InApp",
            Subject = Truncate($"Appointment Update: {appt.ServiceNameAtBooking}", 200),
            Message = $"Your appointment for {appt.ServiceNameAtBooking} at {appt.Salon.Name} status has been updated to '{appt.Status}'.",
            Status = "Sent",
            SentAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        });

        // If status is terminal (Completed, NoShow), skip remaining reminders
        if (appt.Status == "Completed" || appt.Status == "NoShow")
        {
            var pendingReminders = await _dbContext.AppointmentReminders
                .Where(r => r.AppointmentId == appt.Id && r.Status == "Pending")
                .ToListAsync(cancellationToken);

            foreach (var reminder in pendingReminders)
            {
                reminder.Status = "Skipped";
                reminder.LastError = $"Appointment marked as {appt.Status}";
                reminder.ProcessedAtUtc = DateTime.UtcNow;
            }
        }
    }

    private async Task HandlePaymentProofSubmittedAsync(Appointment appt, List<User> salonAdmins, CancellationToken cancellationToken)
    {
        var currency = appt.Salon?.CurrencyCode ?? "INR";
        var method = string.IsNullOrWhiteSpace(appt.PaymentMethod) ? "UPI" : appt.PaymentMethod;
        var txnRef = string.IsNullOrWhiteSpace(appt.PaymentTransactionReference) ? "N/A" : appt.PaymentTransactionReference;

        foreach (var admin in salonAdmins)
        {
            var adminInApp = new Notification
            {
                RecipientUserId = admin.Id,
                SalonId = appt.SalonId,
                AppointmentId = appt.Id,
                Type = "PaymentProofSubmitted",
                Channel = "InApp",
                Subject = Truncate($"Payment Proof: {appt.Customer.FullName} (#{appt.Id})", 200),
                Message = $"{appt.Customer.FullName} submitted {method} payment proof (Ref: {txnRef}, {appt.AmountPaid} {currency}) for appointment #{appt.Id} ({appt.ServiceNameAtBooking}). Please verify.",
                Status = "Sent",
                SentAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow
            };
            _dbContext.Notifications.Add(adminInApp);

            if (!string.IsNullOrWhiteSpace(admin.Email))
            {
                var proofTemplate = _templateBuilder.BuildPaymentProofSubmittedEmail(appt, admin.FullName);
                var emailResult = await _emailService.SendEmailAsync(admin.Email, proofTemplate.Subject, proofTemplate.HtmlBody, proofTemplate.PlainTextBody, cancellationToken);

                _dbContext.Notifications.Add(new Notification
                {
                    RecipientUserId = admin.Id,
                    SalonId = appt.SalonId,
                    AppointmentId = appt.Id,
                    Type = "PaymentProofSubmitted",
                    Channel = "Email",
                    Subject = Truncate(proofTemplate.Subject, 200),
                    Message = TruncateMessage(proofTemplate.PlainTextBody),
                    Status = emailResult.Success ? "Sent" : (emailResult.Skipped ? "Skipped" : "Failed"),
                    SentAtUtc = emailResult.Success ? DateTime.UtcNow : null,
                    ErrorMessage = TruncateNullable(emailResult.ErrorMessage, 1000),
                    LastErrorCode = TruncateNullable(emailResult.ErrorCode, 50),
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }
    }

    private async Task HandlePaymentStatusEventAsync(Appointment appt, List<User> salonAdmins, string eventType, CancellationToken cancellationToken)
    {
        var currency = appt.Salon?.CurrencyCode ?? "INR";
        var salonName = appt.Salon?.Name ?? "the salon";
        var notesSuffix = string.IsNullOrWhiteSpace(appt.PaymentNotes) ? "" : $" Notes: {appt.PaymentNotes}";

        string subject;
        string message;

        if (eventType == "PaymentVerified")
        {
            subject = $"Payment Verified: {appt.ServiceNameAtBooking}";
            message = $"Your payment of {appt.AmountPaid} {currency} for {appt.ServiceNameAtBooking} at {salonName} has been verified as '{appt.PaymentStatus}'.{notesSuffix}";
        }
        else if (eventType == "PaymentRejected")
        {
            subject = $"Payment Not Verified: {appt.ServiceNameAtBooking}";
            message = $"Your payment for {appt.ServiceNameAtBooking} at {salonName} was marked as '{appt.PaymentStatus}'.{notesSuffix} Please verify your transaction details or contact the salon.";
        }
        else if (eventType == "PaymentRefunded")
        {
            subject = $"Payment Refunded: {appt.ServiceNameAtBooking}";
            message = $"A refund of {appt.AmountPaid} {currency} for {appt.ServiceNameAtBooking} at {salonName} has been recorded.{notesSuffix}";
        }
        else
        {
            subject = $"Payment Update: {appt.ServiceNameAtBooking}";
            message = $"Payment status for your appointment ({appt.ServiceNameAtBooking} at {salonName}) is now '{appt.PaymentStatus}'.{notesSuffix}";
        }

        // Customer InApp Notification
        _dbContext.Notifications.Add(new Notification
        {
            RecipientUserId = appt.CustomerId,
            SalonId = appt.SalonId,
            AppointmentId = appt.Id,
            Type = eventType,
            Channel = "InApp",
            Subject = Truncate(subject, 200),
            Message = message,
            Status = "Sent",
            SentAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        });

        // Customer Email Notification
        if (!string.IsNullOrWhiteSpace(appt.Customer.Email))
        {
            var paymentTemplate = _templateBuilder.BuildPaymentStatusEmail(appt, eventType);
            var emailResult = await _emailService.SendEmailAsync(appt.Customer.Email, paymentTemplate.Subject, paymentTemplate.HtmlBody, paymentTemplate.PlainTextBody, cancellationToken);

            _dbContext.Notifications.Add(new Notification
            {
                RecipientUserId = appt.CustomerId,
                SalonId = appt.SalonId,
                AppointmentId = appt.Id,
                Type = eventType,
                Channel = "Email",
                Subject = Truncate(paymentTemplate.Subject, 200),
                Message = TruncateMessage(paymentTemplate.PlainTextBody),
                Status = emailResult.Success ? "Sent" : (emailResult.Skipped ? "Skipped" : "Failed"),
                SentAtUtc = emailResult.Success ? DateTime.UtcNow : null,
                ErrorMessage = TruncateNullable(emailResult.ErrorMessage, 1000),
                LastErrorCode = TruncateNullable(emailResult.ErrorCode, 50),
                CreatedAtUtc = DateTime.UtcNow
            });
        }
    }

    public async Task ProcessReminderAsync(int reminderId, CancellationToken cancellationToken = default)
    {
        var reminder = await _dbContext.AppointmentReminders
            .Include(r => r.Appointment)
                .ThenInclude(a => a.Salon)
            .Include(r => r.Appointment)
                .ThenInclude(a => a.Customer)
            .Include(r => r.Appointment)
                .ThenInclude(a => a.Staff)
            .FirstOrDefaultAsync(r => r.Id == reminderId, cancellationToken);

        if (reminder == null)
            return;

        if (reminder.Status != "Pending")
            return;

        var appt = reminder.Appointment;
        var isPostVisit = reminder.ReminderType == "1HourAfter";

        if (appt == null || appt.Status == "Cancelled" || appt.Status == "NoShow" || (!isPostVisit && appt.Status == "Completed"))
        {
            reminder.Status = "Skipped";
            reminder.LastError = $"Appointment is {appt?.Status ?? "missing"}";
            reminder.ProcessedAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        string reminderNotificationType;
        string inAppSubject;
        string inAppMessage;
        string whatsappSmsMessage;

        var salonAddress = string.Join(", ", new[] { appt.Salon.AddressLine, appt.Salon.City }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var encodedAddress = Uri.EscapeDataString(string.IsNullOrWhiteSpace(salonAddress) ? appt.Salon.Name : salonAddress);
        var mapLink = $"https://maps.google.com/?q={encodedAddress}";

        if (reminder.ReminderType == "24Hours")
        {
            reminderNotificationType = "AppointmentReminder24H";
            inAppSubject = $"Reminder: Upcoming Appointment Tomorrow ({appt.Salon.Name})";
            inAppMessage = $"Your appointment for {appt.ServiceNameAtBooking} with {appt.Staff.FullName} is tomorrow at {appt.StartAtUtc:yyyy-MM-dd HH:mm} UTC. Address: {salonAddress}";
            whatsappSmsMessage = $"👋 Hi {appt.Customer.FullName}! Reminder: Your appointment for {appt.ServiceNameAtBooking} with {appt.Staff.FullName} at {appt.Salon.Name} is tomorrow at {appt.StartAtUtc:HH:mm} UTC.\n📍 Address: {salonAddress} (Map: {mapLink})\n✅ Tap to confirm attendance: {GetWebAppBaseUrl()}/my-bookings?confirm={appt.Id}";
        }
        else if (reminder.ReminderType == "2Hours")
        {
            reminderNotificationType = "AppointmentReminder2H";
            inAppSubject = $"Reminder: Appointment in 2 Hours ({appt.Salon.Name})";
            inAppMessage = $"Your appointment for {appt.ServiceNameAtBooking} with {appt.Staff.FullName} starts in 2 hours at {appt.StartAtUtc:HH:mm} UTC.";
            whatsappSmsMessage = $"⏳ Quick heads-up {appt.Customer.FullName}! Your appointment for {appt.ServiceNameAtBooking} with {appt.Staff.FullName} at {appt.Salon.Name} starts in 2 hours ({appt.StartAtUtc:HH:mm} UTC).\n📍 Directions: {mapLink}\nSee you soon!";
        }
        else // 1HourAfter
        {
            reminderNotificationType = "PostVisitThankYou";
            inAppSubject = $"Thank you for visiting {appt.Salon.Name}! ⭐️ Leave a Review";
            inAppMessage = $"Thank you for choosing {appt.Salon.Name} today for your {appt.ServiceNameAtBooking}! We would love your feedback.";
            whatsappSmsMessage = $"✨ Thank you for visiting {appt.Salon.Name} today, {appt.Customer.FullName}! We hope you loved your {appt.ServiceNameAtBooking} with {appt.Staff.FullName}.\n⭐️ Tap here to leave a 1-tap review: {GetWebAppBaseUrl()}/salons/{appt.Salon.Slug}?review=true\nHave a wonderful day!";
        }

        // 1. InApp Notification
        _dbContext.Notifications.Add(new Notification
        {
            RecipientUserId = appt.CustomerId,
            SalonId = appt.SalonId,
            AppointmentId = appt.Id,
            Type = reminderNotificationType,
            Channel = "InApp",
            Subject = Truncate(inAppSubject, 200),
            Message = Truncate(inAppMessage, 2000),
            Status = "Sent",
            SentAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        });

        // 2. Automated WhatsApp Notification
        _dbContext.Notifications.Add(new Notification
        {
            RecipientUserId = appt.CustomerId,
            SalonId = appt.SalonId,
            AppointmentId = appt.Id,
            Type = reminderNotificationType,
            Channel = "WhatsApp",
            Subject = Truncate(inAppSubject, 200),
            Message = Truncate(whatsappSmsMessage, 2000),
            Status = "Sent",
            SentAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        });

        // 3. Automated SMS Notification
        _dbContext.Notifications.Add(new Notification
        {
            RecipientUserId = appt.CustomerId,
            SalonId = appt.SalonId,
            AppointmentId = appt.Id,
            Type = reminderNotificationType,
            Channel = "SMS",
            Subject = Truncate(inAppSubject, 200),
            Message = Truncate(whatsappSmsMessage, 2000),
            Status = "Sent",
            SentAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        });

        // 4. Email Notification
        if (!string.IsNullOrWhiteSpace(appt.Customer.Email))
        {
            var reminderTemplate = isPostVisit
                ? _templateBuilder.BuildPostVisitReviewEmail(appt)
                : _templateBuilder.BuildAppointmentReminderEmail(appt, reminder.ReminderType);

            var emailResult = await _emailService.SendEmailAsync(appt.Customer.Email, reminderTemplate.Subject, reminderTemplate.HtmlBody, reminderTemplate.PlainTextBody, cancellationToken);

            _dbContext.Notifications.Add(new Notification
            {
                RecipientUserId = appt.CustomerId,
                SalonId = appt.SalonId,
                AppointmentId = appt.Id,
                Type = reminderNotificationType,
                Channel = "Email",
                Subject = Truncate(reminderTemplate.Subject, 200),
                Message = TruncateMessage(reminderTemplate.PlainTextBody),
                Status = emailResult.Success ? "Sent" : (emailResult.Skipped ? "Skipped" : "Failed"),
                SentAtUtc = emailResult.Success ? DateTime.UtcNow : null,
                ErrorMessage = TruncateNullable(emailResult.ErrorMessage, 1000),
                LastErrorCode = TruncateNullable(emailResult.ErrorCode, 50),
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        reminder.Status = "Processed";
        reminder.ProcessedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationResponse>> GetCustomerNotificationsAsync(int customerId, bool? unreadOnly, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientUserId == customerId && n.Channel == "InApp");

        if (unreadOnly == true)
        {
            query = query.Where(n => !n.IsRead);
        }

        return await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .Select(n => new NotificationResponse(
                n.Id,
                n.RecipientUserId,
                n.SalonId,
                n.AppointmentId,
                n.Type,
                n.Channel,
                n.Subject,
                n.Message,
                n.Status,
                n.IsRead,
                n.ReadAtUtc,
                n.SentAtUtc,
                n.CreatedAtUtc
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetCustomerUnreadCountAsync(int customerId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Notifications
            .AsNoTracking()
            .CountAsync(n => n.RecipientUserId == customerId && n.Channel == "InApp" && !n.IsRead, cancellationToken);
    }

    public async Task<bool> MarkNotificationReadAsync(int notificationId, int userId, CancellationToken cancellationToken = default)
    {
        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientUserId == userId, cancellationToken);

        if (notification == null)
            return false;

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<int> MarkAllCustomerNotificationsReadAsync(int customerId, CancellationToken cancellationToken = default)
    {
        var unread = await _dbContext.Notifications
            .Where(n => n.RecipientUserId == customerId && n.Channel == "InApp" && !n.IsRead)
            .ToListAsync(cancellationToken);

        if (unread.Count == 0)
            return 0;

        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return unread.Count;
    }

    public async Task<IReadOnlyList<NotificationResponse>> GetAdminNotificationsAsync(int salonId, int adminUserId, bool? unreadOnly, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.SalonId == salonId && n.RecipientUserId == adminUserId && n.Channel == "InApp");

        if (unreadOnly == true)
        {
            query = query.Where(n => !n.IsRead);
        }

        return await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip(skip)
            .Take(take)
            .Select(n => new NotificationResponse(
                n.Id,
                n.RecipientUserId,
                n.SalonId,
                n.AppointmentId,
                n.Type,
                n.Channel,
                n.Subject,
                n.Message,
                n.Status,
                n.IsRead,
                n.ReadAtUtc,
                n.SentAtUtc,
                n.CreatedAtUtc
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetAdminUnreadCountAsync(int salonId, int adminUserId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Notifications
            .AsNoTracking()
            .CountAsync(n => n.SalonId == salonId && n.RecipientUserId == adminUserId && n.Channel == "InApp" && !n.IsRead, cancellationToken);
    }

    public async Task<int> MarkAllAdminNotificationsReadAsync(int salonId, int adminUserId, CancellationToken cancellationToken = default)
    {
        var unread = await _dbContext.Notifications
            .Where(n => n.SalonId == salonId && n.RecipientUserId == adminUserId && n.Channel == "InApp" && !n.IsRead)
            .ToListAsync(cancellationToken);

        if (unread.Count == 0)
            return 0;

        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAtUtc = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return unread.Count;
    }
}

