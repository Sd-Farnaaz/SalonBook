using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SalonBook.Api.Data;
using Xunit;

namespace SalonBook.Tests;

public class TestDatabaseIsolationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public TestDatabaseIsolationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("Server=db77856.public.databaseasp.net;Database=db77856;User Id=db77856;Password=secret;")]
    [InlineData("Server=sql.databaseasp.net;Database=testdb;User Id=user;Password=pass;")]
    [InlineData("Server=localhost;Database=db77856;Trusted_Connection=True;")]
    [InlineData("Server=beautystudio.runasp.net;Database=some_db;User Id=u;Password=p;")]
    public void SafetyGuard_ThrowsException_WhenProductionConnectionStringDetected(string productionConnStr)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            CustomWebApplicationFactory.ValidateSafeTestConnectionString(productionConnStr));

        Assert.Contains("CRITICAL SAFETY GUARD", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SafetyGuard_ThrowsException_WhenConnectionStringIsNullOrWhitespace(string? emptyConnStr)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            CustomWebApplicationFactory.ValidateSafeTestConnectionString(emptyConnStr));

        Assert.Contains("null or empty", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SafetyGuard_Accepts_LocalTestDatabaseConnectionString()
    {
        var exception = Record.Exception(() =>
            CustomWebApplicationFactory.ValidateSafeTestConnectionString(CustomWebApplicationFactory.DefaultTestConnectionString));

        Assert.Null(exception);
    }

    [Fact]
    public void IntegrationTestFactory_ConfiguresTestingEnvironment_NeverDevelopment()
    {
        var env = _factory.Services.GetRequiredService<IWebHostEnvironment>();

        Assert.Equal("Testing", env.EnvironmentName);
        Assert.False(env.IsDevelopment(), "Integration tests must NOT run under the Development environment where User Secrets are loaded!");
    }

    [Fact]
    public void IntegrationTestFactory_DbContext_PointsToLocalTestDatabase_AndNeverProduction()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var connectionString = dbContext.Database.GetConnectionString();

        Assert.NotNull(connectionString);
        Assert.Contains("MSSQLLocalDB", connectionString, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SalonBook_TestDb", connectionString, StringComparison.OrdinalIgnoreCase);

        // Explicitly assert zero references to any production keywords
        Assert.DoesNotContain("db77856", connectionString, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("databaseasp.net", connectionString, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("runasp.net", connectionString, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("beautystudio", connectionString, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IntegrationTestFactory_Configuration_OverridesProductionSecrets()
    {
        var config = _factory.Services.GetRequiredService<IConfiguration>();

        var connStr = config.GetConnectionString("DefaultConnection");
        Assert.Equal(CustomWebApplicationFactory.DefaultTestConnectionString, connStr);

        var jwtKey = config["Jwt:SecretKey"];
        Assert.Equal("Testing_Integration_Secret_Key_Minimum_32_Characters_Long!", jwtKey);

        var emailEnabled = config["Email:IsEnabled"];
        Assert.Equal("false", emailEnabled);
    }
}
