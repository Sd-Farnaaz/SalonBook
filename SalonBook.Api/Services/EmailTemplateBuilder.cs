using System.Net;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using SalonBook.Api.Configurations;
using SalonBook.Api.Models;

namespace SalonBook.Api.Services;

public class EmailTemplateBuilder : IEmailTemplateBuilder
{
    private readonly EmailOptions _options;
    private readonly IHostEnvironment _env;

    // Brand Palette Constants
    private const string ColorCanvasBg = "#FAF7F5";     // Warm Ivory
    private const string ColorCardBg = "#FFFFFF";       // Card White
    private const string ColorPrimaryRose = "#B66B70";  // Signature Dusty Rose
    private const string ColorDustyRose = "#D9A4B2";    // Soft Accent Rose
    private const string ColorRoseTint = "#FBEFEB";     // Rose Tint Background
    private const string ColorDeepPlum = "#49373A";     // Primary Text
    private const string ColorHeadings = "#2C2022";     // Deep Heading Plum
    private const string ColorMuted = "#7D686B";        // Secondary / Meta Text
    private const string ColorBorder = "#E9DEDA";       // Soft Neutral Border
    private const string ColorSuccess = "#39765B";      // Verified Green
    private const string ColorWarning = "#A86F2C";      // Pending Amber
    private const string ColorDanger = "#B54747";       // Cancelled Red
    private const string ColorInfo = "#456C9A";         // Info Blue

    public EmailTemplateBuilder(IOptions<EmailOptions> options, IHostEnvironment env)
    {
        _options = options.Value;
        _env = env;
    }

    /// <summary>
    /// Returns the verified base URL. In development, defaults to http://localhost:5173.
    /// In production, requires an explicit non-localhost URL to prevent localhost link leakage.
    /// </summary>
    private string? BaseUrl
    {
        get
        {
            var configuredUrl = _options.WebAppBaseUrl?.Trim().TrimEnd('/');

            if (_env.IsDevelopment())
            {
                return string.IsNullOrWhiteSpace(configuredUrl)
                    ? "http://localhost:5173"
                    : configuredUrl;
            }

            // In production/staging: never fall back to localhost or loopback
            if (string.IsNullOrWhiteSpace(configuredUrl) ||
                configuredUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
                configuredUrl.Contains("127.0.0.1"))
            {
                return "https://salonbook-frontend.onrender.com";
            }

            return configuredUrl;
        }
    }

    /// <summary>
    /// Returns the confirmed support/contact email address or null if unconfigured.
    /// Never invents or assumes unverified addresses.
    /// </summary>
    private string? SupportEmail
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_options.SupportEmail))
                return _options.SupportEmail.Trim();

            if (!string.IsNullOrWhiteSpace(_options.SenderEmail))
                return _options.SenderEmail.Trim();

            return null;
        }
    }

    #region 1. Registration Welcome Email

    public EmailTemplateResult BuildCustomerWelcomeEmail(string customerName, string customerEmail)
    {
        var safeName = Encode(string.IsNullOrWhiteSpace(customerName) ? "Valued Guest" : customerName);
        var subject = "Welcome to F&R Beauty Studio — Your Bespoke Beauty Experience";
        var preheader = "Welcome to F&R Beauty Studio. Your sanctuary for bespoke beauty and aesthetic care.";
        var exploreUrl = BaseUrl != null ? $"{BaseUrl}/salons" : null;
        var bookingsUrl = BaseUrl != null ? $"{BaseUrl}/my-bookings" : null;

        var details = new List<(string Label, string Value)>
        {
            ("Account Email", Encode(customerEmail)),
            ("Membership", "Guest Experience"),
            ("Studio Network", "F&R Beauty Studio Partner Salons")
        };

        var leadMessage = $"<p style=\"margin: 0 0 16px 0;\">Dear {safeName},</p>" +
                          $"<p style=\"margin: 0 0 16px 0;\">Welcome to <strong>F&amp;R Beauty Studio</strong>. We are delighted to have you join our community dedicated to refined salon services, luxury aesthetic treatments, and tailored wellness care.</p>" +
                          $"<p style=\"margin: 0;\">Your account is ready. Discover our bespoke services, book appointments seamlessly, and manage your treatments online at any time.</p>";

        var noticeHtml = "<strong>Getting Started Tip:</strong> Browse our verified specialists and choose your preferred treatment, time slot, and customized services in minutes.";

        var secondaryNote = bookingsUrl != null
            ? $"You can also review your upcoming appointments anytime in your <a href=\"{bookingsUrl}\" style=\"color: {ColorPrimaryRose}; text-decoration: underline;\">Customer Dashboard</a>."
            : "You can also review your upcoming appointments anytime in your Customer Dashboard.";

        var html = RenderLayout(new EmailLayoutModel
        {
            Preheader = preheader,
            BadgeText = "Welcome to F&R Beauty Studio",
            BadgeColor = ColorPrimaryRose,
            Headline = "A Sanctuary for Elevated Beauty",
            LeadHtml = leadMessage,
            DetailsRows = details,
            NoticeHtml = noticeHtml,
            NoticeType = NoticeType.Info,
            CtaText = exploreUrl != null ? "Discover Treatments & Book" : null,
            CtaUrl = exploreUrl,
            SecondaryNoteHtml = secondaryNote
        });

        var ptBuilder = new StringBuilder()
            .AppendLine("WELCOME TO F&R BEAUTY STUDIO")
            .AppendLine("==================================================")
            .AppendLine($"Dear {customerName},")
            .AppendLine()
            .AppendLine("Welcome to F&R Beauty Studio. We are delighted to have you join our community dedicated to refined salon services, luxury aesthetic treatments, and tailored wellness care.")
            .AppendLine()
            .AppendLine("Account Email: " + customerEmail)
            .AppendLine("Membership: Guest Experience")
            .AppendLine();

        if (!string.IsNullOrWhiteSpace(exploreUrl)) ptBuilder.AppendLine("Explore Treatments & Book: " + exploreUrl);
        if (!string.IsNullOrWhiteSpace(bookingsUrl)) ptBuilder.AppendLine("My Bookings: " + bookingsUrl);
        if (!string.IsNullOrWhiteSpace(SupportEmail)) ptBuilder.AppendLine("Questions? Contact us at " + SupportEmail);

        ptBuilder.AppendLine("© " + DateTime.UtcNow.Year + " F&R Beauty Studio. All rights reserved.");

        return new EmailTemplateResult(subject, html, ptBuilder.ToString());
    }

    public EmailTemplateResult BuildSalonAdminWelcomeEmail(string adminName, string salonName, string salonSlug)
    {
        var safeAdminName = Encode(string.IsNullOrWhiteSpace(adminName) ? "Partner" : adminName);
        var safeSalonName = Encode(salonName);
        var subject = $"Welcome to F&R Beauty Studio — Onboarding for {salonName}";
        var preheader = $"Welcome to the F&R Beauty Studio platform. Manage {salonName} effortlessly.";
        var dashboardUrl = BaseUrl != null ? $"{BaseUrl}/admin/dashboard" : null;

        var details = new List<(string Label, string Value)>
        {
            ("Studio / Salon", safeSalonName),
            ("Unique Handle", Encode(salonSlug)),
            ("Administrator", safeAdminName),
            ("Platform Access", "Active Partner")
        };

        var leadMessage = $"<p style=\"margin: 0 0 16px 0;\">Dear {safeAdminName},</p>" +
                          $"<p style=\"margin: 0 0 16px 0;\">Welcome to the <strong>F&amp;R Beauty Studio</strong> business platform. Your studio <strong>{safeSalonName}</strong> is registered and ready for configuration.</p>" +
                          $"<p style=\"margin: 0;\">From your salon dashboard, you can configure services, manage specialist schedules, track live client bookings, and review payment settlements seamlessly.</p>";

        var noticeHtml = "<strong>Next Steps:</strong> Complete your salon profile, add specialists and working hours, and activate your services to start receiving client bookings.";

        var html = RenderLayout(new EmailLayoutModel
        {
            Preheader = preheader,
            BadgeText = "Partner Onboarding",
            BadgeColor = ColorPrimaryRose,
            Headline = $"Welcome {safeSalonName}",
            LeadHtml = leadMessage,
            DetailsRows = details,
            NoticeHtml = noticeHtml,
            NoticeType = NoticeType.Success,
            CtaText = dashboardUrl != null ? "Open Salon Dashboard" : null,
            CtaUrl = dashboardUrl
        });

        var ptBuilder = new StringBuilder()
            .AppendLine("WELCOME TO F&R BEAUTY STUDIO FOR PARTNERS")
            .AppendLine("==================================================")
            .AppendLine($"Dear {adminName},")
            .AppendLine()
            .AppendLine($"Your studio '{salonName}' has been successfully registered with F&R Beauty Studio.")
            .AppendLine()
            .AppendLine("Studio: " + salonName)
            .AppendLine("Handle: " + salonSlug)
            .AppendLine("Admin: " + adminName)
            .AppendLine();

        if (!string.IsNullOrWhiteSpace(dashboardUrl)) ptBuilder.AppendLine("Access Dashboard: " + dashboardUrl);
        if (!string.IsNullOrWhiteSpace(SupportEmail)) ptBuilder.AppendLine("Support Contact: " + SupportEmail);

        ptBuilder.AppendLine("© " + DateTime.UtcNow.Year + " F&R Beauty Studio. All rights reserved.");

        return new EmailTemplateResult(subject, html, ptBuilder.ToString());
    }

    #endregion

    #region 2. Booking Confirmation Email

    public EmailTemplateResult BuildBookingConfirmationEmail(Appointment appt)
    {
        var customerName = string.IsNullOrWhiteSpace(appt.Customer?.FullName) ? "Valued Guest" : appt.Customer.FullName;
        var salonName = string.IsNullOrWhiteSpace(appt.Salon?.Name) ? "F&R Beauty Studio" : appt.Salon.Name;
        var serviceName = string.IsNullOrWhiteSpace(appt.ServiceNameAtBooking) ? "Beauty Treatment" : appt.ServiceNameAtBooking;
        var staffName = string.IsNullOrWhiteSpace(appt.Staff?.FullName) ? "Specialist Assigned" : appt.Staff.FullName;
        var formattedDate = appt.StartAtUtc.ToString("dddd, MMMM d, yyyy 'at' h:mm tt 'UTC'");
        var currency = appt.Salon?.CurrencyCode ?? "INR";
        var priceDisplay = $"{appt.PriceAtBooking:N2} {currency}";
        var address = FormatAddress(appt.Salon);

        var subject = $"Booking Confirmed: {serviceName} at {salonName}";
        var preheader = $"Your appointment for {serviceName} with {staffName} on {formattedDate} is confirmed.";
        var bookingsUrl = BaseUrl != null ? $"{BaseUrl}/my-bookings" : null;

        var details = new List<(string Label, string Value)>
        {
            ("Treatment", Encode(serviceName)),
            ("Specialist", Encode(staffName)),
            ("Date & Time", formattedDate),
            ("Studio Location", Encode(address)),
            ("Appointment Reference", $"#{appt.Id}"),
            ("Total Price", priceDisplay),
            ("Booking Status", Encode(appt.Status))
        };

        if (appt.AdvanceAmountRequired > 0)
        {
            details.Add(("Advance Payment", $"{appt.AdvanceAmountRequired:N2} {currency} ({appt.PaymentStatus})"));
        }

        var leadHtml = $"<p style=\"margin: 0 0 16px 0;\">Dear {Encode(customerName)},</p>" +
                       $"<p style=\"margin: 0 0 16px 0;\">Your appointment at <strong>{Encode(salonName)}</strong> has been confirmed. Our team is dedicated to providing you with an exceptional, personalized experience.</p>" +
                       $"<p style=\"margin: 0;\">Please review your appointment summary below:</p>";

        var noticeHtml = "<strong>Preparation Note:</strong> Please arrive 5 to 10 minutes prior to your scheduled treatment time to enjoy a relaxed, unhurried consultation.";

        var html = RenderLayout(new EmailLayoutModel
        {
            Preheader = preheader,
            BadgeText = "Appointment Confirmed",
            BadgeColor = ColorSuccess,
            Headline = "Your Beauty Treatment is Booked",
            LeadHtml = leadHtml,
            DetailsRows = details,
            NoticeHtml = noticeHtml,
            NoticeType = NoticeType.Success,
            CtaText = bookingsUrl != null ? "View Booking Details" : null,
            CtaUrl = bookingsUrl,
            SecondaryNoteHtml = "Need to make changes? You can manage your appointments directly through your customer dashboard."
        });

        var ptBuilder = new StringBuilder()
            .AppendLine("F&R BEAUTY STUDIO — APPOINTMENT CONFIRMATION")
            .AppendLine("==================================================")
            .AppendLine($"Dear {customerName},")
            .AppendLine()
            .AppendLine($"Your appointment for {serviceName} at {salonName} has been confirmed.")
            .AppendLine()
            .AppendLine("DETAILS:")
            .AppendLine("--------------------------------------------------")
            .AppendLine("Treatment: " + serviceName)
            .AppendLine("Specialist: " + staffName)
            .AppendLine("Date & Time: " + formattedDate)
            .AppendLine("Location: " + address)
            .AppendLine("Reference: #" + appt.Id)
            .AppendLine("Total: " + priceDisplay)
            .AppendLine("Status: " + appt.Status)
            .AppendLine();

        if (!string.IsNullOrWhiteSpace(bookingsUrl)) ptBuilder.AppendLine("Manage Booking: " + bookingsUrl);
        if (!string.IsNullOrWhiteSpace(SupportEmail)) ptBuilder.AppendLine("Support: " + SupportEmail);

        ptBuilder.AppendLine("© " + DateTime.UtcNow.Year + " F&R Beauty Studio. All rights reserved.");

        return new EmailTemplateResult(subject, html, ptBuilder.ToString());
    }

    #endregion

    #region 3. Salon-Owner New Booking Notification

    public EmailTemplateResult BuildOwnerNewBookingEmail(Appointment appt, string? adminName = null)
    {
        var recipientAdmin = string.IsNullOrWhiteSpace(adminName) ? "Partner" : adminName;
        var customerName = string.IsNullOrWhiteSpace(appt.Customer?.FullName) ? "Client" : appt.Customer.FullName;
        var serviceName = string.IsNullOrWhiteSpace(appt.ServiceNameAtBooking) ? "Service" : appt.ServiceNameAtBooking;
        var staffName = string.IsNullOrWhiteSpace(appt.Staff?.FullName) ? "Unassigned" : appt.Staff.FullName;
        var formattedDate = appt.StartAtUtc.ToString("dddd, MMMM d, yyyy 'at' h:mm tt 'UTC'");
        var currency = appt.Salon?.CurrencyCode ?? "INR";
        var priceDisplay = $"{appt.PriceAtBooking:N2} {currency}";

        var subject = $"[New Booking] {customerName} — {serviceName} ({formattedDate})";
        var preheader = $"New client booking received from {customerName} for {serviceName}.";
        var adminUrl = BaseUrl != null ? $"{BaseUrl}/admin/appointments" : null;

        var details = new List<(string Label, string Value)>
        {
            ("Client Name", Encode(customerName)),
            ("Client Email", Encode(appt.Customer?.Email ?? "N/A")),
            ("Service Booked", Encode(serviceName)),
            ("Specialist Assigned", Encode(staffName)),
            ("Scheduled For", formattedDate),
            ("Booking ID", $"#{appt.Id}"),
            ("Gross Amount", priceDisplay),
            ("Payment Status", Encode(appt.PaymentStatus))
        };

        if (!string.IsNullOrWhiteSpace(appt.CustomerNotes))
        {
            details.Add(("Client Notes", Encode(appt.CustomerNotes)));
        }

        var leadHtml = $"<p style=\"margin: 0 0 16px 0;\">Dear {Encode(recipientAdmin)},</p>" +
                       $"<p style=\"margin: 0 0 16px 0;\">A new appointment has been scheduled at your studio by <strong>{Encode(customerName)}</strong>.</p>" +
                       $"<p style=\"margin: 0;\">Please review the booking schedule and ensure specialist availability.</p>";

        var html = RenderLayout(new EmailLayoutModel
        {
            Preheader = preheader,
            BadgeText = "New Client Booking",
            BadgeColor = ColorPrimaryRose,
            Headline = "New Appointment Scheduled",
            LeadHtml = leadHtml,
            DetailsRows = details,
            CtaText = adminUrl != null ? "Open Admin Calendar" : null,
            CtaUrl = adminUrl
        });

        var ptBuilder = new StringBuilder()
            .AppendLine("F&R BEAUTY STUDIO — NEW APPOINTMENT NOTIFICATION")
            .AppendLine("==================================================")
            .AppendLine($"Hello {recipientAdmin},")
            .AppendLine()
            .AppendLine($"A new appointment has been booked by {customerName} for {serviceName}.")
            .AppendLine()
            .AppendLine("Client: " + customerName)
            .AppendLine("Email: " + (appt.Customer?.Email ?? "N/A"))
            .AppendLine("Service: " + serviceName)
            .AppendLine("Specialist: " + staffName)
            .AppendLine("Date & Time: " + formattedDate)
            .AppendLine("Booking ID: #" + appt.Id)
            .AppendLine("Total: " + priceDisplay)
            .AppendLine();

        if (!string.IsNullOrWhiteSpace(adminUrl)) ptBuilder.AppendLine("Calendar: " + adminUrl);

        ptBuilder.AppendLine("© " + DateTime.UtcNow.Year + " F&R Beauty Studio. All rights reserved.");

        return new EmailTemplateResult(subject, html, ptBuilder.ToString());
    }

    #endregion

    #region 4. Appointment Reminders (24-Hour & 2-Hour)

    public EmailTemplateResult BuildAppointmentReminderEmail(Appointment appt, string reminderType)
    {
        var customerName = string.IsNullOrWhiteSpace(appt.Customer?.FullName) ? "Valued Guest" : appt.Customer.FullName;
        var salonName = string.IsNullOrWhiteSpace(appt.Salon?.Name) ? "F&R Beauty Studio" : appt.Salon.Name;
        var serviceName = string.IsNullOrWhiteSpace(appt.ServiceNameAtBooking) ? "Beauty Treatment" : appt.ServiceNameAtBooking;
        var staffName = string.IsNullOrWhiteSpace(appt.Staff?.FullName) ? "Specialist Assigned" : appt.Staff.FullName;
        var formattedDate = appt.StartAtUtc.ToString("dddd, MMMM d, yyyy 'at' h:mm tt 'UTC'");
        var address = FormatAddress(appt.Salon);
        var bookingsUrl = BaseUrl != null ? $"{BaseUrl}/my-bookings" : null;

        bool is2Hours = reminderType == "2Hours";
        var timeframe = is2Hours ? "in 2 hours" : "tomorrow";
        var badgeText = is2Hours ? "Starting in 2 Hours" : "Upcoming Tomorrow";

        var subject = is2Hours
            ? $"Reminder: Your appointment at {salonName} starts in 2 hours"
            : $"Reminder: Your appointment tomorrow at {salonName}";

        var preheader = $"Friendly reminder for your {serviceName} appointment with {staffName} at {salonName} {timeframe}.";

        var details = new List<(string Label, string Value)>
        {
            ("Treatment", Encode(serviceName)),
            ("Specialist", Encode(staffName)),
            ("Scheduled Time", formattedDate),
            ("Studio Location", Encode(address)),
            ("Reference ID", $"#{appt.Id}")
        };

        var leadHtml = $"<p style=\"margin: 0 0 16px 0;\">Dear {Encode(customerName)},</p>" +
                       $"<p style=\"margin: 0 0 16px 0;\">This is a gentle reminder that your appointment for <strong>{Encode(serviceName)}</strong> at <strong>{Encode(salonName)}</strong> is scheduled {timeframe}.</p>" +
                       $"<p style=\"margin: 0;\">Our team is preparing for your visit to ensure a tranquil and luxurious experience.</p>";

        var noticeHtml = is2Hours
            ? "<strong>Arrival Reminder:</strong> We look forward to welcoming you shortly. Please arrive 5 minutes early to unwind before your treatment begins."
            : "<strong>Studio Etiquette:</strong> Please arrive 5 to 10 minutes early. If you require rescheduling, please let us know as early as possible.";

        var html = RenderLayout(new EmailLayoutModel
        {
            Preheader = preheader,
            BadgeText = badgeText,
            BadgeColor = is2Hours ? ColorWarning : ColorInfo,
            Headline = is2Hours ? "Your Treatment Starts Soon" : "We Look Forward to Seeing You Tomorrow",
            LeadHtml = leadHtml,
            DetailsRows = details,
            NoticeHtml = noticeHtml,
            NoticeType = is2Hours ? NoticeType.Warning : NoticeType.Info,
            CtaText = bookingsUrl != null ? "View Booking & Directions" : null,
            CtaUrl = bookingsUrl
        });

        var ptBuilder = new StringBuilder()
            .AppendLine($"F&R BEAUTY STUDIO — APPOINTMENT REMINDER ({timeframe.ToUpperInvariant()})")
            .AppendLine("==================================================")
            .AppendLine($"Dear {customerName},")
            .AppendLine()
            .AppendLine($"Friendly reminder that your {serviceName} appointment at {salonName} is scheduled {timeframe} at {formattedDate}.")
            .AppendLine()
            .AppendLine("Treatment: " + serviceName)
            .AppendLine("Specialist: " + staffName)
            .AppendLine("Time: " + formattedDate)
            .AppendLine("Location: " + address)
            .AppendLine("Reference: #" + appt.Id)
            .AppendLine();

        if (!string.IsNullOrWhiteSpace(bookingsUrl)) ptBuilder.AppendLine("Manage Booking: " + bookingsUrl);
        if (!string.IsNullOrWhiteSpace(SupportEmail)) ptBuilder.AppendLine("Support: " + SupportEmail);

        ptBuilder.AppendLine("© " + DateTime.UtcNow.Year + " F&R Beauty Studio. All rights reserved.");

        return new EmailTemplateResult(subject, html, ptBuilder.ToString());
    }

    #endregion

    #region 5. Appointment Cancellation Email

    public EmailTemplateResult BuildAppointmentCancelledEmail(Appointment appt, bool isForCustomer, string? adminName = null)
    {
        var customerName = string.IsNullOrWhiteSpace(appt.Customer?.FullName) ? "Valued Guest" : appt.Customer.FullName;
        var salonName = string.IsNullOrWhiteSpace(appt.Salon?.Name) ? "F&R Beauty Studio" : appt.Salon.Name;
        var serviceName = string.IsNullOrWhiteSpace(appt.ServiceNameAtBooking) ? "Beauty Treatment" : appt.ServiceNameAtBooking;
        var staffName = string.IsNullOrWhiteSpace(appt.Staff?.FullName) ? "Specialist Assigned" : appt.Staff.FullName;
        var formattedDate = appt.StartAtUtc.ToString("dddd, MMMM d, yyyy 'at' h:mm tt 'UTC'");
        var reason = string.IsNullOrWhiteSpace(appt.CancellationReason) ? "No reason specified" : appt.CancellationReason;

        var details = new List<(string Label, string Value)>
        {
            ("Treatment", Encode(serviceName)),
            ("Specialist", Encode(staffName)),
            ("Cancelled Time", formattedDate),
            ("Studio Location", Encode(FormatAddress(appt.Salon))),
            ("Reference ID", $"#{appt.Id}"),
            ("Cancellation Reason", Encode(reason))
        };

        if (isForCustomer)
        {
            var subject = $"Appointment Cancelled: {serviceName} at {salonName}";
            var preheader = $"Your appointment for {serviceName} on {formattedDate} has been cancelled.";
            var exploreUrl = BaseUrl != null ? $"{BaseUrl}/salons" : null;

            var leadHtml = $"<p style=\"margin: 0 0 16px 0;\">Dear {Encode(customerName)},</p>" +
                           $"<p style=\"margin: 0 0 16px 0;\">Your appointment for <strong>{Encode(serviceName)}</strong> at <strong>{Encode(salonName)}</strong> has been cancelled.</p>" +
                           $"<p style=\"margin: 0;\">If any advance payment was made, our team will process eligible refunds according to studio cancellation policies.</p>";

            var noticeHtml = "<strong>Would you like to reschedule?</strong> You can explore alternative dates and book a new treatment with our specialists whenever you are ready.";

            var html = RenderLayout(new EmailLayoutModel
            {
                Preheader = preheader,
                BadgeText = "Appointment Cancelled",
                BadgeColor = ColorDanger,
                Headline = "Your Appointment Has Been Cancelled",
                LeadHtml = leadHtml,
                DetailsRows = details,
                NoticeHtml = noticeHtml,
                NoticeType = NoticeType.Neutral,
                CtaText = exploreUrl != null ? "Explore Available Treatments" : null,
                CtaUrl = exploreUrl
            });

            var ptBuilder = new StringBuilder()
                .AppendLine("F&R BEAUTY STUDIO — APPOINTMENT CANCELLATION")
                .AppendLine("==================================================")
                .AppendLine($"Dear {customerName},")
                .AppendLine()
                .AppendLine($"Your appointment for {serviceName} at {salonName} ({formattedDate}) has been cancelled.")
                .AppendLine("Reason: " + reason)
                .AppendLine();

            if (!string.IsNullOrWhiteSpace(exploreUrl)) ptBuilder.AppendLine("Rebook at your convenience: " + exploreUrl);
            if (!string.IsNullOrWhiteSpace(SupportEmail)) ptBuilder.AppendLine("Support: " + SupportEmail);

            ptBuilder.AppendLine("© " + DateTime.UtcNow.Year + " F&R Beauty Studio. All rights reserved.");

            return new EmailTemplateResult(subject, html, ptBuilder.ToString());
        }
        else
        {
            var adminDisplayName = string.IsNullOrWhiteSpace(adminName) ? "Partner" : adminName;
            var subject = $"[Cancelled] Appointment #{appt.Id} — {customerName} ({serviceName})";
            var preheader = $"Appointment #{appt.Id} for {customerName} was cancelled. Reason: {reason}.";
            var adminUrl = BaseUrl != null ? $"{BaseUrl}/admin/appointments" : null;

            details.Insert(0, ("Client Name", Encode(customerName)));

            var leadHtml = $"<p style=\"margin: 0 0 16px 0;\">Dear {Encode(adminDisplayName)},</p>" +
                           $"<p style=\"margin: 0 0 16px 0;\">Appointment <strong>#{appt.Id}</strong> with <strong>{Encode(customerName)}</strong> for <strong>{Encode(serviceName)}</strong> has been cancelled.</p>" +
                           $"<p style=\"margin: 0;\">The allocated time slot on your staff schedule has been released for new bookings.</p>";

            var html = RenderLayout(new EmailLayoutModel
            {
                Preheader = preheader,
                BadgeText = "Booking Cancelled",
                BadgeColor = ColorDanger,
                Headline = "Appointment Slot Released",
                LeadHtml = leadHtml,
                DetailsRows = details,
                CtaText = adminUrl != null ? "View Admin Schedule" : null,
                CtaUrl = adminUrl
            });

            var ptBuilder = new StringBuilder()
                .AppendLine("F&R BEAUTY STUDIO — APPOINTMENT CANCELLED (ADMIN NOTIFICATION)")
                .AppendLine("==================================================")
                .AppendLine($"Hello {adminDisplayName},")
                .AppendLine()
                .AppendLine($"Appointment #{appt.Id} for {customerName} ({serviceName}) scheduled for {formattedDate} was cancelled.")
                .AppendLine("Reason: " + reason)
                .AppendLine();

            if (!string.IsNullOrWhiteSpace(adminUrl)) ptBuilder.AppendLine("Schedule: " + adminUrl);

            ptBuilder.AppendLine("© " + DateTime.UtcNow.Year + " F&R Beauty Studio. All rights reserved.");

            return new EmailTemplateResult(subject, html, ptBuilder.ToString());
        }
    }

    #endregion

    #region 6. Appointment Rescheduling Email

    public EmailTemplateResult BuildAppointmentRescheduledEmail(Appointment appt, bool isForCustomer, string? adminName = null, DateTime? previousStartUtc = null)
    {
        var customerName = string.IsNullOrWhiteSpace(appt.Customer?.FullName) ? "Valued Guest" : appt.Customer.FullName;
        var salonName = string.IsNullOrWhiteSpace(appt.Salon?.Name) ? "F&R Beauty Studio" : appt.Salon.Name;
        var serviceName = string.IsNullOrWhiteSpace(appt.ServiceNameAtBooking) ? "Beauty Treatment" : appt.ServiceNameAtBooking;
        var staffName = string.IsNullOrWhiteSpace(appt.Staff?.FullName) ? "Specialist Assigned" : appt.Staff.FullName;
        var newDateFormatted = appt.StartAtUtc.ToString("dddd, MMMM d, yyyy 'at' h:mm tt 'UTC'");
        var address = FormatAddress(appt.Salon);

        var details = new List<(string Label, string Value)>
        {
            ("Treatment", Encode(serviceName)),
            ("Specialist", Encode(staffName)),
            ("New Date & Time", $"<strong style=\"color: {ColorPrimaryRose};\">{newDateFormatted}</strong>"),
            ("Studio Location", Encode(address)),
            ("Reference ID", $"#{appt.Id}")
        };

        if (previousStartUtc.HasValue)
        {
            var oldDateFormatted = previousStartUtc.Value.ToString("dddd, MMMM d, yyyy 'at' h:mm tt 'UTC'");
            details.Insert(2, ("Previous Schedule", $"<span style=\"text-decoration: line-through; color: {ColorMuted};\">{oldDateFormatted}</span>"));
        }

        if (isForCustomer)
        {
            var subject = $"Appointment Rescheduled: {serviceName} at {salonName}";
            var preheader = $"Your appointment for {serviceName} has been rescheduled to {newDateFormatted}.";
            var bookingsUrl = BaseUrl != null ? $"{BaseUrl}/my-bookings" : null;

            var leadHtml = $"<p style=\"margin: 0 0 16px 0;\">Dear {Encode(customerName)},</p>" +
                           $"<p style=\"margin: 0 0 16px 0;\">Your appointment for <strong>{Encode(serviceName)}</strong> at <strong>{Encode(salonName)}</strong> has been rescheduled.</p>" +
                           $"<p style=\"margin: 0;\">Please review your updated schedule details below:</p>";

            var noticeHtml = "<strong>Please Note:</strong> Your appointment reference number and payment records remain unchanged. We look forward to welcoming you at your updated time.";

            var html = RenderLayout(new EmailLayoutModel
            {
                Preheader = preheader,
                BadgeText = "Schedule Updated",
                BadgeColor = ColorInfo,
                Headline = "Your Appointment Has Been Rescheduled",
                LeadHtml = leadHtml,
                DetailsRows = details,
                NoticeHtml = noticeHtml,
                NoticeType = NoticeType.Info,
                CtaText = bookingsUrl != null ? "Review Updated Booking" : null,
                CtaUrl = bookingsUrl
            });

            var ptBuilder = new StringBuilder()
                .AppendLine("F&R BEAUTY STUDIO — APPOINTMENT RESCHEDULED")
                .AppendLine("==================================================")
                .AppendLine($"Dear {customerName},")
                .AppendLine()
                .AppendLine($"Your appointment for {serviceName} at {salonName} has been rescheduled.")
                .AppendLine("New Time: " + newDateFormatted)
                .AppendLine("Specialist: " + staffName)
                .AppendLine("Location: " + address)
                .AppendLine("Reference: #" + appt.Id)
                .AppendLine();

            if (!string.IsNullOrWhiteSpace(bookingsUrl)) ptBuilder.AppendLine("Review Booking: " + bookingsUrl);
            if (!string.IsNullOrWhiteSpace(SupportEmail)) ptBuilder.AppendLine("Support: " + SupportEmail);

            ptBuilder.AppendLine("© " + DateTime.UtcNow.Year + " F&R Beauty Studio. All rights reserved.");

            return new EmailTemplateResult(subject, html, ptBuilder.ToString());
        }
        else
        {
            var adminDisplayName = string.IsNullOrWhiteSpace(adminName) ? "Partner" : adminName;
            var subject = $"[Rescheduled] Appointment #{appt.Id} — {customerName} ({serviceName})";
            var preheader = $"Appointment #{appt.Id} for {customerName} has been moved to {newDateFormatted}.";
            var adminUrl = BaseUrl != null ? $"{BaseUrl}/admin/appointments" : null;

            details.Insert(0, ("Client Name", Encode(customerName)));

            var leadHtml = $"<p style=\"margin: 0 0 16px 0;\">Dear {Encode(adminDisplayName)},</p>" +
                           $"<p style=\"margin: 0 0 16px 0;\">Appointment <strong>#{appt.Id}</strong> with <strong>{Encode(customerName)}</strong> has been rescheduled on your studio schedule.</p>" +
                           $"<p style=\"margin: 0;\">Your calendar and specialist roster have been updated accordingly.</p>";

            var html = RenderLayout(new EmailLayoutModel
            {
                Preheader = preheader,
                BadgeText = "Schedule Adjusted",
                BadgeColor = ColorInfo,
                Headline = "Appointment Schedule Updated",
                LeadHtml = leadHtml,
                DetailsRows = details,
                CtaText = adminUrl != null ? "View in Calendar" : null,
                CtaUrl = adminUrl
            });

            var ptBuilder = new StringBuilder()
                .AppendLine("F&R BEAUTY STUDIO — APPOINTMENT RESCHEDULED (ADMIN)")
                .AppendLine("==================================================")
                .AppendLine($"Hello {adminDisplayName},")
                .AppendLine()
                .AppendLine($"Appointment #{appt.Id} for {customerName} has been moved to {newDateFormatted}.")
                .AppendLine("Staff: " + staffName)
                .AppendLine();

            if (!string.IsNullOrWhiteSpace(adminUrl)) ptBuilder.AppendLine("Schedule: " + adminUrl);

            ptBuilder.AppendLine("© " + DateTime.UtcNow.Year + " F&R Beauty Studio. All rights reserved.");

            return new EmailTemplateResult(subject, html, ptBuilder.ToString());
        }
    }

    #endregion

    #region 7. Payment Verification / Status Updates

    public EmailTemplateResult BuildPaymentStatusEmail(Appointment appt, string eventType)
    {
        var customerName = string.IsNullOrWhiteSpace(appt.Customer?.FullName) ? "Valued Guest" : appt.Customer.FullName;
        var salonName = string.IsNullOrWhiteSpace(appt.Salon?.Name) ? "F&R Beauty Studio" : appt.Salon.Name;
        var serviceName = string.IsNullOrWhiteSpace(appt.ServiceNameAtBooking) ? "Beauty Treatment" : appt.ServiceNameAtBooking;
        var currency = appt.Salon?.CurrencyCode ?? "INR";
        var amountDisplay = $"{appt.AmountPaid:N2} {currency}";
        var bookingsUrl = BaseUrl != null ? $"{BaseUrl}/my-bookings" : null;

        string badgeText;
        string badgeColor;
        string headline;
        string subject;
        string preheader;
        string leadMessage;
        string noticeHtml;
        NoticeType noticeType;

        var details = new List<(string Label, string Value)>
        {
            ("Treatment", Encode(serviceName)),
            ("Studio", Encode(salonName)),
            ("Amount", amountDisplay),
            ("Payment Method", Encode(string.IsNullOrWhiteSpace(appt.PaymentMethod) ? "Electronic Payment" : appt.PaymentMethod)),
            ("Reference ID", $"#{appt.Id}")
        };

        if (!string.IsNullOrWhiteSpace(appt.PaymentTransactionReference))
        {
            details.Add(("Transaction Reference", Encode(appt.PaymentTransactionReference)));
        }

        if (eventType == "PaymentVerified")
        {
            badgeText = "Payment Verified";
            badgeColor = ColorSuccess;
            headline = "Your Payment Has Been Verified";
            subject = $"Payment Verified: {serviceName} at {salonName}";
            preheader = $"Your payment of {amountDisplay} for {serviceName} has been verified successfully.";
            leadMessage = $"<p style=\"margin: 0 0 16px 0;\">Dear {Encode(customerName)},</p>" +
                          $"<p style=\"margin: 0 0 16px 0;\">We are pleased to confirm that your payment of <strong>{amountDisplay}</strong> for your upcoming treatment at <strong>{Encode(salonName)}</strong> has been verified.</p>" +
                          $"<p style=\"margin: 0;\">Your booking is secured and ready for your visit.</p>";
            noticeHtml = "<strong>All Set:</strong> No further payment action is required for this transaction. We look forward to serving you.";
            noticeType = NoticeType.Success;
            details.Add(("Verification Status", "<strong style=\"color: " + ColorSuccess + ";\">Verified</strong>"));
        }
        else if (eventType == "PaymentRejected")
        {
            badgeText = "Verification Needed";
            badgeColor = ColorWarning;
            headline = "Payment Verification Update";
            subject = $"Payment Verification Update: {serviceName} at {salonName}";
            preheader = $"We were unable to verify your payment proof for {serviceName}.";
            leadMessage = $"<p style=\"margin: 0 0 16px 0;\">Dear {Encode(customerName)},</p>" +
                          $"<p style=\"margin: 0 0 16px 0;\">Our studio administration was unable to reconcile the submitted payment proof of <strong>{amountDisplay}</strong> for appointment <strong>#{appt.Id}</strong>.</p>" +
                          $"<p style=\"margin: 0;\">Please review your transaction reference or contact the studio directly to resolve this.</p>";
            noticeHtml = string.IsNullOrWhiteSpace(appt.PaymentNotes)
                ? "<strong>Next Steps:</strong> Please log in to your dashboard to re-verify your transaction reference or settle upon arrival."
                : $"<strong>Studio Note:</strong> {Encode(appt.PaymentNotes)}";
            noticeType = NoticeType.Warning;
            details.Add(("Verification Status", "<strong style=\"color: " + ColorWarning + ";\">Action Required</strong>"));
        }
        else if (eventType == "PaymentRefunded")
        {
            badgeText = "Refund Processed";
            badgeColor = ColorInfo;
            headline = "Refund Confirmation";
            subject = $"Refund Processed: {serviceName} at {salonName}";
            preheader = $"A refund of {amountDisplay} for your appointment #{appt.Id} has been processed.";
            leadMessage = $"<p style=\"margin: 0 0 16px 0;\">Dear {Encode(customerName)},</p>" +
                          $"<p style=\"margin: 0 0 16px 0;\">A refund of <strong>{amountDisplay}</strong> has been issued for your appointment <strong>#{appt.Id}</strong> ({Encode(serviceName)}) at <strong>{Encode(salonName)}</strong>.</p>" +
                          $"<p style=\"margin: 0;\">Depending on your payment method, funds typically reflect in your account within 3 to 5 business days.</p>";
            noticeHtml = "<strong>Assistance:</strong> If you do not see the refunded amount after 5 business days, please contact our support team.";
            noticeType = NoticeType.Info;
            details.Add(("Verification Status", "<strong style=\"color: " + ColorInfo + ";\">Refunded</strong>"));
        }
        else
        {
            badgeText = "Payment Update";
            badgeColor = ColorPrimaryRose;
            headline = "Payment Status Notice";
            subject = $"Payment Notice: {serviceName} at {salonName}";
            preheader = $"Payment update regarding appointment #{appt.Id}.";
            leadMessage = $"<p style=\"margin: 0 0 16px 0;\">Dear {Encode(customerName)},</p>" +
                          $"<p style=\"margin: 0 0 16px 0;\">Your payment status for appointment <strong>#{appt.Id}</strong> ({Encode(serviceName)}) has been updated to <strong>{Encode(appt.PaymentStatus)}</strong>.</p>";
            noticeHtml = string.IsNullOrWhiteSpace(appt.PaymentNotes) ? "Please check your account for details." : Encode(appt.PaymentNotes);
            noticeType = NoticeType.Neutral;
        }

        var html = RenderLayout(new EmailLayoutModel
        {
            Preheader = preheader,
            BadgeText = badgeText,
            BadgeColor = badgeColor,
            Headline = headline,
            LeadHtml = leadMessage,
            DetailsRows = details,
            NoticeHtml = noticeHtml,
            NoticeType = noticeType,
            CtaText = bookingsUrl != null ? "View in Dashboard" : null,
            CtaUrl = bookingsUrl
        });

        var ptBuilder = new StringBuilder()
            .AppendLine("F&R BEAUTY STUDIO — PAYMENT NOTIFICATION")
            .AppendLine("==================================================")
            .AppendLine($"Dear {customerName},")
            .AppendLine()
            .AppendLine(headline)
            .AppendLine("Treatment: " + serviceName)
            .AppendLine("Studio: " + salonName)
            .AppendLine("Amount: " + amountDisplay)
            .AppendLine("Reference: #" + appt.Id)
            .AppendLine();

        if (!string.IsNullOrWhiteSpace(bookingsUrl)) ptBuilder.AppendLine("Dashboard: " + bookingsUrl);
        if (!string.IsNullOrWhiteSpace(SupportEmail)) ptBuilder.AppendLine("Support: " + SupportEmail);

        ptBuilder.AppendLine("© " + DateTime.UtcNow.Year + " F&R Beauty Studio. All rights reserved.");

        return new EmailTemplateResult(subject, html, ptBuilder.ToString());
    }

    public EmailTemplateResult BuildPaymentProofSubmittedEmail(Appointment appt, string? adminName = null)
    {
        var adminDisplayName = string.IsNullOrWhiteSpace(adminName) ? "Partner" : adminName;
        var customerName = string.IsNullOrWhiteSpace(appt.Customer?.FullName) ? "Client" : appt.Customer.FullName;
        var serviceName = string.IsNullOrWhiteSpace(appt.ServiceNameAtBooking) ? "Beauty Treatment" : appt.ServiceNameAtBooking;
        var currency = appt.Salon?.CurrencyCode ?? "INR";
        var amountDisplay = $"{appt.AmountPaid:N2} {currency}";
        var posUrl = BaseUrl != null ? $"{BaseUrl}/admin/pos" : null;

        var subject = $"[Payment Proof] Appointment #{appt.Id} — {customerName} ({amountDisplay})";
        var preheader = $"Client {customerName} submitted payment verification proof for appointment #{appt.Id}.";

        var details = new List<(string Label, string Value)>
        {
            ("Client Name", Encode(customerName)),
            ("Appointment ID", $"#{appt.Id}"),
            ("Treatment", Encode(serviceName)),
            ("Proof Amount", amountDisplay),
            ("Payment Method", Encode(string.IsNullOrWhiteSpace(appt.PaymentMethod) ? "UPI / Bank Transfer" : appt.PaymentMethod)),
            ("Transaction Reference", Encode(string.IsNullOrWhiteSpace(appt.PaymentTransactionReference) ? "Not Provided" : appt.PaymentTransactionReference))
        };

        if (!string.IsNullOrWhiteSpace(appt.PaymentNotes))
        {
            details.Add(("Client Memo", Encode(appt.PaymentNotes)));
        }

        var leadHtml = $"<p style=\"margin: 0 0 16px 0;\">Dear {Encode(adminDisplayName)},</p>" +
                       $"<p style=\"margin: 0 0 16px 0;\">Client <strong>{Encode(customerName)}</strong> has submitted payment verification proof for appointment <strong>#{appt.Id}</strong>.</p>" +
                       $"<p style=\"margin: 0;\">Please review the transaction reference in your Point of Sale dashboard and verify the payment.</p>";

        var html = RenderLayout(new EmailLayoutModel
        {
            Preheader = preheader,
            BadgeText = "Payment Proof Pending",
            BadgeColor = ColorWarning,
            Headline = "Client Submitted Payment Proof",
            LeadHtml = leadHtml,
            DetailsRows = details,
            NoticeHtml = "<strong>Action Required:</strong> Log in to cross-check transaction references against your studio UPI/Bank account records.",
            NoticeType = NoticeType.Warning,
            CtaText = posUrl != null ? "Review in Admin POS" : null,
            CtaUrl = posUrl
        });

        var ptBuilder = new StringBuilder()
            .AppendLine("F&R BEAUTY STUDIO — PAYMENT PROOF SUBMITTED (ADMIN)")
            .AppendLine("==================================================")
            .AppendLine($"Hello {adminDisplayName},")
            .AppendLine()
            .AppendLine($"Client {customerName} submitted payment proof for appointment #{appt.Id} ({amountDisplay}).")
            .AppendLine("Reference: " + (appt.PaymentTransactionReference ?? "N/A"))
            .AppendLine();

        if (!string.IsNullOrWhiteSpace(posUrl)) ptBuilder.AppendLine("POS Dashboard: " + posUrl);

        ptBuilder.AppendLine("© " + DateTime.UtcNow.Year + " F&R Beauty Studio. All rights reserved.");

        return new EmailTemplateResult(subject, html, ptBuilder.ToString());
    }

    #endregion

    #region 8. Password Reset Email

    public EmailTemplateResult BuildPasswordResetEmail(string recipientName, string resetCode, int expirationMinutes = 30)
    {
        var safeName = Encode(string.IsNullOrWhiteSpace(recipientName) ? "Valued Guest" : recipientName);
        var subject = "Reset Your F&R Beauty Studio Password";
        var preheader = $"Use security code {resetCode} to reset your F&R Beauty Studio password. Valid for {expirationMinutes} minutes.";
        var resetUrl = BaseUrl != null ? $"{BaseUrl}/forgot-password" : null;

        var leadHtml = $"<p style=\"margin: 0 0 16px 0;\">Dear {safeName},</p>" +
                       $"<p style=\"margin: 0 0 16px 0;\">We received a request to reset the password for your <strong>F&amp;R Beauty Studio</strong> account.</p>" +
                       $"<p style=\"margin: 0;\">Use the one-time verification code below to authorize the password reset:</p>";

        var codeBoxHtml = $"<table cellpadding=\"0\" cellspacing=\"0\" border=\"0\" align=\"center\" style=\"margin: 24px auto;\">" +
                          $"<tr>" +
                          $"<td align=\"center\" style=\"padding: 16px 32px; background-color: {ColorRoseTint}; border: 1.5px dashed {ColorPrimaryRose}; border-radius: 12px;\">" +
                          $"<div style=\"font-family: 'Playfair Display', Georgia, serif; font-size: 32px; font-weight: 700; letter-spacing: 6px; color: {ColorHeadings};\">{Encode(resetCode)}</div>" +
                          $"<div style=\"font-family: -apple-system, sans-serif; font-size: 11px; text-transform: uppercase; letter-spacing: 1.5px; color: {ColorMuted}; margin-top: 6px;\">Expires in {expirationMinutes} Minutes</div>" +
                          $"</td>" +
                          $"</tr>" +
                          $"</table>";

        var noticeHtml = "<strong>Security Advisory:</strong> If you did not initiate this request, you can safely disregard this email. Your password will remain unchanged and your account is secure.";

        var html = RenderLayout(new EmailLayoutModel
        {
            Preheader = preheader,
            BadgeText = "Account Security",
            BadgeColor = ColorPrimaryRose,
            Headline = "Password Reset Request",
            LeadHtml = leadHtml + codeBoxHtml,
            NoticeHtml = noticeHtml,
            NoticeType = NoticeType.Neutral,
            CtaText = resetUrl != null ? "Reset My Password" : null,
            CtaUrl = resetUrl,
            SecondaryNoteHtml = "For security reasons, this verification code can only be used once and expires automatically."
        });

        var ptBuilder = new StringBuilder()
            .AppendLine("F&R BEAUTY STUDIO — PASSWORD RESET REQUEST")
            .AppendLine("==================================================")
            .AppendLine($"Dear {recipientName},")
            .AppendLine()
            .AppendLine("We received a request to reset your F&R Beauty Studio account password.")
            .AppendLine()
            .AppendLine("YOUR VERIFICATION CODE: " + resetCode)
            .AppendLine($"This code will expire in {expirationMinutes} minutes.")
            .AppendLine();

        if (!string.IsNullOrWhiteSpace(resetUrl)) ptBuilder.AppendLine("Reset Password Page: " + resetUrl);
        if (!string.IsNullOrWhiteSpace(SupportEmail)) ptBuilder.AppendLine("Support: " + SupportEmail);

        ptBuilder.AppendLine("© " + DateTime.UtcNow.Year + " F&R Beauty Studio. All rights reserved.");

        return new EmailTemplateResult(subject, html, ptBuilder.ToString());
    }

    #endregion

    #region 9. Post-Treatment Review Request

    public EmailTemplateResult BuildPostVisitReviewEmail(Appointment appt)
    {
        var customerName = string.IsNullOrWhiteSpace(appt.Customer?.FullName) ? "Valued Guest" : appt.Customer.FullName;
        var salonName = string.IsNullOrWhiteSpace(appt.Salon?.Name) ? "F&R Beauty Studio" : appt.Salon.Name;
        var serviceName = string.IsNullOrWhiteSpace(appt.ServiceNameAtBooking) ? "Beauty Treatment" : appt.ServiceNameAtBooking;
        var staffName = string.IsNullOrWhiteSpace(appt.Staff?.FullName) ? "our specialist" : appt.Staff.FullName;

        // In SalonBook, verified customer reviews are submitted through the verified booking dashboard (/my-bookings)
        var reviewUrl = BaseUrl != null ? $"{BaseUrl}/my-bookings" : null;

        var subject = $"How was your experience at {salonName}? ⭐️ F&R Beauty Studio";
        var preheader = $"Thank you for visiting {salonName} today! We would love your feedback on your {serviceName} treatment.";

        var details = new List<(string Label, string Value)>
        {
            ("Treatment", Encode(serviceName)),
            ("Specialist", Encode(staffName)),
            ("Studio Visited", Encode(salonName)),
            ("Visit Date", appt.StartAtUtc.ToString("MMMM d, yyyy"))
        };

        var leadHtml = $"<p style=\"margin: 0 0 16px 0;\">Dear {Encode(customerName)},</p>" +
                       $"<p style=\"margin: 0 0 16px 0;\">Thank you for visiting <strong>{Encode(salonName)}</strong> today for your <strong>{Encode(serviceName)}</strong> treatment with {Encode(staffName)}.</p>" +
                       $"<p style=\"margin: 0;\">Our specialists take immense pride in crafting exceptional experiences. We would be grateful if you could share a few words about your visit.</p>";

        var starsHtml = $"<table cellpadding=\"0\" cellspacing=\"0\" border=\"0\" align=\"center\" style=\"margin: 20px auto 10px auto;\">" +
                        $"<tr>" +
                        $"<td align=\"center\" style=\"font-size: 26px; color: #D4AF37; letter-spacing: 6px;\">★ ★ ★ ★ ★</td>" +
                        $"</tr>" +
                        $"</table>";

        var noticeHtml = "<strong>Your Voice Matters:</strong> Your honest review helps our specialists refine their craft and guides fellow guests seeking their next self-care experience.";

        var html = RenderLayout(new EmailLayoutModel
        {
            Preheader = preheader,
            BadgeText = "Guest Experience Feedback",
            BadgeColor = ColorPrimaryRose,
            Headline = "We Value Your Thoughts",
            LeadHtml = leadHtml + starsHtml,
            DetailsRows = details,
            NoticeHtml = noticeHtml,
            NoticeType = NoticeType.Info,
            CtaText = reviewUrl != null ? "Leave a Verified Review" : null,
            CtaUrl = reviewUrl,
            SecondaryNoteHtml = "It only takes 60 seconds and helps us maintain the highest standard of salon hospitality."
        });

        var ptBuilder = new StringBuilder()
            .AppendLine("F&R BEAUTY STUDIO — HOW WAS YOUR EXPERIENCE?")
            .AppendLine("==================================================")
            .AppendLine($"Dear {customerName},")
            .AppendLine()
            .AppendLine($"Thank you for visiting {salonName} today for your {serviceName} treatment.")
            .AppendLine("We would love to know how your visit was.")
            .AppendLine();

        if (!string.IsNullOrWhiteSpace(reviewUrl)) ptBuilder.AppendLine("Leave your review: " + reviewUrl);
        if (!string.IsNullOrWhiteSpace(SupportEmail)) ptBuilder.AppendLine("Support: " + SupportEmail);

        ptBuilder.AppendLine("© " + DateTime.UtcNow.Year + " F&R Beauty Studio. All rights reserved.");

        return new EmailTemplateResult(subject, html, ptBuilder.ToString());
    }

    #endregion

    #region 10. Internal SMTP Diagnostic Test Email

    public EmailTemplateResult BuildInternalSmtpTestEmail(string recipientEmail)
    {
        var safeRecipient = Encode(recipientEmail);
        var subject = "[F&R Beauty Studio] Internal SMTP Test Verification";
        var preheader = "Diagnostic system test: Brevo SMTP connection verified successfully.";
        var nowUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");

        var details = new List<(string Label, string Value)>
        {
            ("Service Host", Encode(_options.SmtpHost)),
            ("SMTP Port", _options.SmtpPort.ToString()),
            ("Security Transport", _options.EnableSsl ? "STARTTLS (Explicit SSL/TLS)" : "Plain (Insecure)"),
            ("Sender Address", Encode(_options.SenderEmail)),
            ("Sender Display Name", Encode(_options.SenderName)),
            ("Recipient Verified", safeRecipient),
            ("Execution Timestamp", nowUtc)
        };

        var leadHtml = $"<p style=\"margin: 0 0 16px 0;\">Hello Administrator,</p>" +
                       $"<p style=\"margin: 0 0 16px 0;\">This is an <strong>Internal Administrative Diagnostic Email</strong> sent from your <strong>F&amp;R Beauty Studio</strong> ASP.NET Core backend platform via <strong>Brevo SMTP</strong>.</p>" +
                       $"<p style=\"margin: 0;\">Receiving this message confirms that your SMTP credentials, TLS security handshake, and sender domain verification are properly configured and operational.</p>";

        var noticeHtml = "<strong>Diagnostic Notice:</strong> This message is strictly for internal infrastructure verification. It is never dispatched to customers or salon guests.";

        var ctaUrl = _env.IsDevelopment()
            ? "http://localhost:5220/swagger"
            : (BaseUrl != null ? $"{BaseUrl}/superadmin/dashboard" : null);

        var ctaText = _env.IsDevelopment() ? "Open Swagger Documentation" : "Open SuperAdmin Dashboard";

        var html = RenderLayout(new EmailLayoutModel
        {
            Preheader = preheader,
            BadgeText = "Internal System Diagnostic",
            BadgeColor = ColorInfo,
            Headline = "F&R Beauty Studio — Internal SMTP Test",
            LeadHtml = leadHtml,
            DetailsRows = details,
            NoticeHtml = noticeHtml,
            NoticeType = NoticeType.Info,
            CtaText = ctaUrl != null ? ctaText : null,
            CtaUrl = ctaUrl
        });

        var ptBuilder = new StringBuilder()
            .AppendLine("F&R BEAUTY STUDIO — INTERNAL SMTP TEST")
            .AppendLine("==================================================")
            .AppendLine("Diagnostic system test: Brevo SMTP connection verified successfully.")
            .AppendLine()
            .AppendLine("Host: " + _options.SmtpHost)
            .AppendLine("Port: " + _options.SmtpPort)
            .AppendLine("SSL: " + (_options.EnableSsl ? "STARTTLS" : "Disabled"))
            .AppendLine("Sender: " + _options.SenderEmail)
            .AppendLine("Recipient: " + recipientEmail)
            .AppendLine("Timestamp: " + nowUtc)
            .AppendLine();

        if (!string.IsNullOrWhiteSpace(ctaUrl)) ptBuilder.AppendLine("Portal: " + ctaUrl);

        ptBuilder.AppendLine()
            .AppendLine("Note: This is strictly an internal administrative test message.")
            .AppendLine("© " + DateTime.UtcNow.Year + " F&R Beauty Studio. All rights reserved.");

        return new EmailTemplateResult(subject, html, ptBuilder.ToString());
    }

    #endregion

    #region Layout & Rendering Helpers

    private enum NoticeType
    {
        Info,
        Success,
        Warning,
        Neutral
    }

    private class EmailLayoutModel
    {
        public string Preheader { get; set; } = string.Empty;
        public string BadgeText { get; set; } = string.Empty;
        public string BadgeColor { get; set; } = ColorPrimaryRose;
        public string Headline { get; set; } = string.Empty;
        public string LeadHtml { get; set; } = string.Empty;
        public List<(string Label, string Value)>? DetailsRows { get; set; }
        public string? NoticeHtml { get; set; }
        public NoticeType NoticeType { get; set; } = NoticeType.Neutral;
        public string? CtaText { get; set; }
        public string? CtaUrl { get; set; }
        public string? SecondaryNoteHtml { get; set; }
    }

    private string RenderLayout(EmailLayoutModel model)
    {
        var sb = new StringBuilder();

        sb.Append("<!DOCTYPE html PUBLIC \"-//W3C//DTD XHTML 1.0 Transitional//EN\" \"http://www.w3.org/TR/xhtml1/DTD/xhtml1-transitional.dtd\">\n");
        sb.Append("<html xmlns=\"http://www.w3.org/1999/xhtml\" lang=\"en\">\n");
        sb.Append("<head>\n");
        sb.Append("  <meta http-equiv=\"Content-Type\" content=\"text/html; charset=UTF-8\" />\n");
        sb.Append("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\" />\n");
        sb.Append("  <meta name=\"x-apple-disable-message-reformatting\" />\n");
        sb.Append("  <title>F&amp;R Beauty Studio</title>\n");
        sb.Append("  <style type=\"text/css\">\n");
        sb.Append("    body { margin: 0; padding: 0; -webkit-text-size-adjust: 100%; -ms-text-size-adjust: 100%; }\n");
        sb.Append("    table, td { border-collapse: collapse; mso-table-lspace: 0pt; mso-table-rspace: 0pt; }\n");
        sb.Append("    img { border: 0; height: auto; line-height: 100%; outline: none; text-decoration: none; }\n");
        sb.Append("    @media only screen and (max-width: 620px) {\n");
        sb.Append("      .container-table { width: 100% !important; border-radius: 0 !important; }\n");
        sb.Append("      .content-padding { padding: 24px 20px !important; }\n");
        sb.Append("      .header-padding { padding: 28px 20px 16px 20px !important; }\n");
        sb.Append("      .footer-padding { padding: 20px !important; }\n");
        sb.Append("      .cta-button { width: 100% !important; text-align: center !important; }\n");
        sb.Append("    }\n");
        sb.Append("  </style>\n");
        sb.Append("</head>\n");
        sb.Append($"<body style=\"margin: 0; padding: 0; background-color: {ColorCanvasBg}; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: {ColorDeepPlum};\">\n");

        // Hidden preheader for email inbox preview
        if (!string.IsNullOrWhiteSpace(model.Preheader))
        {
            sb.Append($"  <div style=\"display: none; max-height: 0px; overflow: hidden; mso-hide: all; font-size: 1px; line-height: 1px; color: #FAF7F5; opacity: 0;\">\n");
            sb.Append($"    {Encode(model.Preheader)} &zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;&zwnj;&nbsp;\n");
            sb.Append("  </div>\n");
        }

        // Outer Background Wrapper
        sb.Append($"  <table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"background-color: {ColorCanvasBg}; padding: 32px 12px;\">\n");
        sb.Append("    <tr>\n");
        sb.Append("      <td align=\"center\" valign=\"top\">\n");

        // Centered Card Container
        sb.Append($"        <table class=\"container-table\" width=\"600\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"max-width: 600px; width: 100%; background-color: {ColorCardBg}; border: 1px solid {ColorBorder}; border-radius: 16px; overflow: hidden; box-shadow: 0 4px 16px rgba(73, 55, 58, 0.05);\">\n");

        // Top Accent Color Line (Dusty Rose)
        sb.Append("          <tr>\n");
        sb.Append($"            <td height=\"4\" style=\"background-color: {ColorPrimaryRose}; font-size: 1px; line-height: 1px;\">&nbsp;</td>\n");
        sb.Append("          </tr>\n");

        // Brand Header (Official Logo Image with alt & text branding)
        sb.Append("          <tr>\n");
        sb.Append("            <td class=\"header-padding\" align=\"center\" style=\"padding: 32px 36px 20px 36px;\">\n");
        sb.Append(RenderBrandHeader());
        sb.Append("            </td>\n");
        sb.Append("          </tr>\n");

        // Soft Divider Under Header
        sb.Append("          <tr>\n");
        sb.Append("            <td style=\"padding: 0 36px;\">\n");
        sb.Append($"              <div style=\"height: 1px; background-color: {ColorBorder}; line-height: 1px; font-size: 1px;\">&nbsp;</div>\n");
        sb.Append("            </td>\n");
        sb.Append("          </tr>\n");

        // Content Body
        sb.Append("          <tr>\n");
        sb.Append("            <td class=\"content-padding\" style=\"padding: 28px 36px 32px 36px;\">\n");

        // Badge Pill
        if (!string.IsNullOrWhiteSpace(model.BadgeText))
        {
            sb.Append("              <div style=\"margin-bottom: 14px;\">\n");
            sb.Append($"                <span style=\"display: inline-block; padding: 5px 14px; border-radius: 20px; font-size: 11px; font-weight: 700; text-transform: uppercase; letter-spacing: 1.2px; background-color: {ColorRoseTint}; color: {model.BadgeColor}; border: 1px solid {ColorBorder};\">\n");
            sb.Append($"                  {Encode(model.BadgeText)}\n");
            sb.Append("                </span>\n");
            sb.Append("              </div>\n");
        }

        // Main Headline
        if (!string.IsNullOrWhiteSpace(model.Headline))
        {
            sb.Append($"              <h1 style=\"font-family: 'Playfair Display', Georgia, 'Times New Roman', serif; font-size: 24px; font-weight: 700; color: {ColorHeadings}; margin: 0 0 16px 0; line-height: 1.35;\">\n");
            sb.Append($"                {Encode(model.Headline)}\n");
            sb.Append("              </h1>\n");
        }

        // Lead Paragraph / Narrative
        if (!string.IsNullOrWhiteSpace(model.LeadHtml))
        {
            sb.Append($"              <div style=\"font-size: 14.5px; line-height: 1.65; color: {ColorDeepPlum}; margin-bottom: 24px;\">\n");
            sb.Append($"                {model.LeadHtml}\n");
            sb.Append("              </div>\n");
        }

        // Details Key-Value Table
        if (model.DetailsRows != null && model.DetailsRows.Count > 0)
        {
            sb.Append(RenderDetailsCard(model.DetailsRows));
        }

        // Notice / Advisory Callout Box
        if (!string.IsNullOrWhiteSpace(model.NoticeHtml))
        {
            sb.Append(RenderNoticeBox(model.NoticeHtml, model.NoticeType));
        }

        // Call to Action Button (Only rendered if both text and URL are non-empty)
        if (!string.IsNullOrWhiteSpace(model.CtaText) && !string.IsNullOrWhiteSpace(model.CtaUrl))
        {
            sb.Append("              <table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin: 28px 0 16px 0;\">\n");
            sb.Append("                <tr>\n");
            sb.Append("                  <td align=\"center\">\n");
            sb.Append($"                    <a class=\"cta-button\" href=\"{model.CtaUrl}\" style=\"display: inline-block; background-color: {ColorPrimaryRose}; color: #FFFFFF; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; font-size: 14px; font-weight: 600; text-decoration: none; padding: 13px 32px; border-radius: 8px; letter-spacing: 0.5px; box-shadow: 0 2px 6px rgba(182, 107, 112, 0.25);\">\n");
            sb.Append($"                      {Encode(model.CtaText)}\n");
            sb.Append("                    </a>\n");
            sb.Append("                  </td>\n");
            sb.Append("                </tr>\n");
            sb.Append("              </table>\n");
        }

        // Secondary Note
        if (!string.IsNullOrWhiteSpace(model.SecondaryNoteHtml))
        {
            sb.Append($"              <p style=\"font-size: 12.5px; color: {ColorMuted}; line-height: 1.5; margin: 20px 0 0 0; text-align: center;\">\n");
            sb.Append($"                {model.SecondaryNoteHtml}\n");
            sb.Append("              </p>\n");
        }

        sb.Append("            </td>\n");
        sb.Append("          </tr>\n");

        // Brand Footer
        sb.Append("          <tr>\n");
        sb.Append($"            <td class=\"footer-padding\" align=\"center\" style=\"background-color: {ColorCanvasBg}; border-top: 1px solid {ColorBorder}; padding: 26px 36px; text-align: center;\">\n");
        sb.Append(RenderBrandFooter());
        sb.Append("            </td>\n");
        sb.Append("          </tr>\n");

        sb.Append("        </table>\n");
        sb.Append("      </td>\n");
        sb.Append("    </tr>\n");
        sb.Append("  </table>\n");
        sb.Append("</body>\n");
        sb.Append("</html>");

        return sb.ToString();
    }

    /// <summary>
    /// Renders the official brand logo with responsive email-safe sizing and alt-text fallback.
    /// Preserves original proportions without cropping or distorting.
    /// </summary>
    private string RenderBrandHeader()
    {
        var logoUrl = !string.IsNullOrWhiteSpace(_options.LogoUrl)
            ? _options.LogoUrl.Trim()
            : "https://drive.google.com/thumbnail?id=16DzxD4lir8jaDDqvYVNTdTwC7LgUyU13&sz=w500";

        var logoImgHtml = $"<img src=\"{Encode(logoUrl)}\" alt=\"F&amp;R Beauty Studio\" width=\"100\" height=\"100\" border=\"0\" style=\"display: block; width: 100px; max-width: 100px; height: auto; border: 0; outline: none; text-decoration: none; margin: 0 auto; -ms-interpolation-mode: bicubic; font-family: 'Playfair Display', Georgia, serif; font-size: 16px; font-weight: 700; color: {ColorHeadings};\" />";

        return $"<table cellpadding=\"0\" cellspacing=\"0\" border=\"0\" align=\"center\" style=\"margin: 0 auto; text-align: center;\">\n" +
               $"  <tr>\n" +
               $"    <td align=\"center\" style=\"padding-bottom: 12px;\">\n" +
               $"      {logoImgHtml}\n" +
               $"    </td>\n" +
               $"  </tr>\n" +
               $"  <tr>\n" +
               $"    <td align=\"center\">\n" +
               $"      <span style=\"font-family: 'Playfair Display', Georgia, 'Times New Roman', serif; font-size: 22px; font-weight: 700; color: {ColorHeadings}; letter-spacing: 0.5px;\">F&amp;R <span style=\"color: {ColorPrimaryRose};\">Beauty Studio</span></span>\n" +
               $"    </td>\n" +
               $"  </tr>\n" +
               $"  <tr>\n" +
               $"    <td align=\"center\" style=\"padding-top: 3px;\">\n" +
               $"      <span style=\"font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; font-size: 10px; font-weight: 600; color: {ColorMuted}; text-transform: uppercase; letter-spacing: 2px;\">Elevated Beauty &amp; Wellness</span>\n" +
               $"    </td>\n" +
               $"  </tr>\n" +
               $"</table>";
    }

    private string RenderBrandFooter()
    {
        var year = DateTime.UtcNow.Year;
        var supportLine = !string.IsNullOrWhiteSpace(SupportEmail)
            ? $"Questions or assistance? <a href=\"mailto:{SupportEmail}\" style=\"color: {ColorPrimaryRose}; text-decoration: none; font-weight: 600;\">{Encode(SupportEmail)}</a>"
            : "Questions or assistance? Please contact our studio management";

        var portalLine = !string.IsNullOrWhiteSpace(BaseUrl)
            ? $" &bull; <a href=\"{BaseUrl}\" style=\"color: {ColorMuted}; text-decoration: underline;\">Visit Studio Portal</a>"
            : "";

        return $"<p style=\"margin: 0 0 6px 0; font-family: 'Playfair Display', Georgia, serif; font-size: 14px; font-weight: 700; color: {ColorHeadings};\">F&amp;R Beauty Studio</p>\n" +
               $"<p style=\"margin: 0 0 12px 0; font-size: 12px; color: {ColorMuted};\">Bespoke Salon, Hair &amp; Aesthetic Care</p>\n" +
               $"<p style=\"margin: 0 0 12px 0; font-size: 12px; color: {ColorMuted};\">\n" +
               $"  {supportLine}{portalLine}\n" +
               $"</p>\n" +
               $"<p style=\"margin: 0; font-size: 11px; color: #A29497; line-height: 1.4;\">\n" +
               $"  &copy; {year} F&amp;R Beauty Studio. All rights reserved.<br />\n" +
               $"  This is an automated transactional notification regarding your account or appointment activity.\n" +
               $"</p>";
    }

    private string RenderDetailsCard(List<(string Label, string Value)> rows)
    {
        var sb = new StringBuilder();
        sb.Append($"<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"background-color: {ColorCanvasBg}; border: 1px solid {ColorBorder}; border-radius: 12px; margin: 20px 0; overflow: hidden;\">\n");

        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var borderBottom = (i < rows.Count - 1) ? $"border-bottom: 1px solid {ColorBorder};" : "";

            sb.Append("  <tr>\n");
            sb.Append($"    <td valign=\"top\" style=\"padding: 12px 18px; width: 38%; font-size: 12px; font-weight: 700; color: {ColorMuted}; text-transform: uppercase; letter-spacing: 0.8px; {borderBottom}\">\n");
            sb.Append($"      {Encode(row.Label)}\n");
            sb.Append("    </td>\n");
            sb.Append($"    <td valign=\"top\" align=\"right\" style=\"padding: 12px 18px; width: 62%; font-size: 13.5px; font-weight: 500; color: {ColorHeadings}; {borderBottom}\">\n");
            sb.Append($"      {row.Value}\n");
            sb.Append("    </td>\n");
            sb.Append("  </tr>\n");
        }

        sb.Append("</table>\n");
        return sb.ToString();
    }

    private string RenderNoticeBox(string contentHtml, NoticeType type)
    {
        string borderColor;
        string bgColor;

        switch (type)
        {
            case NoticeType.Success:
                borderColor = ColorSuccess;
                bgColor = "#F0F8F4";
                break;
            case NoticeType.Warning:
                borderColor = ColorWarning;
                bgColor = "#FAF3E8";
                break;
            case NoticeType.Info:
                borderColor = ColorInfo;
                bgColor = "#EFF5FA";
                break;
            default:
                borderColor = ColorPrimaryRose;
                bgColor = ColorRoseTint;
                break;
        }

        return $"<table width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\" style=\"margin: 20px 0; background-color: {bgColor}; border-left: 4px solid {borderColor}; border-radius: 6px;\">\n" +
               $"  <tr>\n" +
               $"    <td style=\"padding: 14px 18px; font-size: 13px; line-height: 1.55; color: {ColorDeepPlum};\">\n" +
               $"      {contentHtml}\n" +
               $"    </td>\n" +
               $"  </tr>\n" +
               $"</table>\n";
    }

    private static string FormatAddress(Salon? salon)
    {
        if (salon == null) return "F&R Beauty Studio";

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(salon.AddressLine)) parts.Add(salon.AddressLine.Trim());
        if (!string.IsNullOrWhiteSpace(salon.City)) parts.Add(salon.City.Trim());
        if (!string.IsNullOrWhiteSpace(salon.PostalCode)) parts.Add(salon.PostalCode.Trim());

        return parts.Count > 0 ? string.Join(", ", parts) : (salon.Name ?? "F&R Beauty Studio");
    }

    private static string Encode(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return WebUtility.HtmlEncode(input);
    }

    #endregion
}
