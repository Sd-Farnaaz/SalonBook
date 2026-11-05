using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SalonBook.Api.Data;
using SalonBook.Api.Services;

namespace SalonBook.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string DefaultTestConnectionString =
        "Server=(localdb)\\MSSQLLocalDB;Database=SalonBook_TestDb;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True;";

    public const string DefaultTestJwtSecretKey =
        "Testing_Integration_Secret_Key_Minimum_32_Characters_Long!";

    private static readonly object _initLock = new();
    private static bool _databaseInitialized = false;

    /// <summary>
    /// Strict safety guard that prevents running integration tests against live production databases.
    /// Throws InvalidOperationException if any production database hostname, database name, or domain is detected.
    /// </summary>
    public static void ValidateSafeTestConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("CRITICAL SAFETY GUARD: Test connection string is null or empty!");
        }

        var normalized = connectionString.ToLowerInvariant();
        string[] prohibitedTerms = { "db77856", "databaseasp.net", "runasp.net", "beautystudio" };

        foreach (var term in prohibitedTerms)
        {
            if (normalized.Contains(term))
            {
                throw new InvalidOperationException(
                    $"CRITICAL SAFETY GUARD TRIGGERED: Integration tests REFUSE to run against production database! Prohibited term '{term}' detected in connection string.");
            }
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Enforce dedicated "Testing" environment so Development User Secrets are never loaded
        builder.UseEnvironment("Testing");

        // UseSetting supplies early configuration to WebApplicationBuilder before Program.cs evaluates JwtOptions
        builder.UseSetting("ConnectionStrings:DefaultConnection", DefaultTestConnectionString);
        builder.UseSetting("Jwt:SecretKey", DefaultTestJwtSecretKey);
        builder.UseSetting("Jwt:Issuer", "SalonBook.Testing");
        builder.UseSetting("Jwt:Audience", "SalonBook.Testing.Client");
        builder.UseSetting("Email:IsEnabled", "false");
        builder.UseSetting("Email:WebAppBaseUrl", "http://localhost:5173");
        builder.UseSetting("Razorpay:KeyId", "rzp_test_IntegrationKey");
        builder.UseSetting("Razorpay:KeySecret", "rzp_test_IntegrationSecret");
        builder.UseSetting("Razorpay:WebhookSecret", "rzp_test_IntegrationWebhookSecret");

        builder.ConfigureAppConfiguration((context, configBuilder) =>
        {
            // Explicitly override configuration dictionary with isolated local test settings
            var testConfig = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = DefaultTestConnectionString,
                ["Jwt:SecretKey"] = DefaultTestJwtSecretKey,
                ["Jwt:Issuer"] = "SalonBook.Testing",
                ["Jwt:Audience"] = "SalonBook.Testing.Client",
                ["Email:IsEnabled"] = "false",
                ["Email:WebAppBaseUrl"] = "http://localhost:5173",
                ["Razorpay:KeyId"] = "rzp_test_IntegrationKey",
                ["Razorpay:KeySecret"] = "rzp_test_IntegrationSecret",
                ["Razorpay:WebhookSecret"] = "rzp_test_IntegrationWebhookSecret"
            };

            configBuilder.AddInMemoryCollection(testConfig);
        });

        builder.ConfigureServices(services =>
        {
            // Remove any previously registered DbContext / DbContextOptions
            var descriptorsToRemove = services
                .Where(d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                            d.ServiceType == typeof(ApplicationDbContext) ||
                            d.ServiceType == typeof(DbContextOptions))
                .ToList();

            foreach (var descriptor in descriptorsToRemove)
            {
                services.Remove(descriptor);
            }

            // Verify the test connection string passes the safety guard
            ValidateSafeTestConnectionString(DefaultTestConnectionString);

            // Re-register ApplicationDbContext with the verified isolated local test database
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                options.UseSqlServer(DefaultTestConnectionString, sqlServerOptions =>
                {
                    sqlServerOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null);
                });
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = builder.Build();

        lock (_initLock)
        {
            if (!_databaseInitialized)
            {
                using var scope = host.Services.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var safetyInitializer = scope.ServiceProvider.GetRequiredService<IDatabaseSafetyInitializer>();

                var actualConnStr = dbContext.Database.GetConnectionString();
                ValidateSafeTestConnectionString(actualConnStr);

                // Ensure local test database and schema exist
                dbContext.Database.EnsureCreated();
                safetyInitializer.EnsureSchemaSafetyAsync().GetAwaiter().GetResult();

                _databaseInitialized = true;
            }
        }

        host.Start();
        return host;
    }
}
