using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SalonBook.Api.Common;
using SalonBook.Api.DTOs;
using SalonBook.Api.Services;

namespace SalonBook.Tests;

public class AuthenticationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthenticationIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    private async Task SeedSuperAdminAsync(string email, string password)
    {
        using var scope = _factory.Services.CreateScope();
        var provisioningService = scope.ServiceProvider.GetRequiredService<ISuperAdminProvisioningService>();
        await provisioningService.ProvisionSuperAdminAsync(email, "Super Administrator", password);
    }

    [Fact]
    public async Task SuperAdminLogin_Succeeds_AndRejectsAdminAndCustomerLogins()
    {
        var saEmail = "sa_" + Guid.NewGuid().ToString("N") + "@salonbook.com";
        var saPass = "SAPassword123!";
        await SeedSuperAdminAsync(saEmail, saPass);

        // 1. Successful SuperAdmin Login
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/superadmin/login", new LoginRequest
        {
            Email = saEmail,
            Password = saPass
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        var authResult = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authResult);
        Assert.NotEmpty(authResult.AccessToken);
        Assert.Equal(Roles.SuperAdmin, authResult.User.Role);
        Assert.Null(authResult.User.SalonId);

        // 2. SuperAdmin attempting to login via Admin endpoint is rejected (401)
        var adminEndpointResponse = await _client.PostAsJsonAsync("/api/auth/admin/login", new LoginRequest
        {
            Email = saEmail,
            Password = saPass
        });
        Assert.Equal(HttpStatusCode.Unauthorized, adminEndpointResponse.StatusCode);

        // 3. SuperAdmin attempting to login via User endpoint is rejected (401)
        var userEndpointResponse = await _client.PostAsJsonAsync("/api/auth/user/login", new LoginRequest
        {
            Email = saEmail,
            Password = saPass
        });
        Assert.Equal(HttpStatusCode.Unauthorized, userEndpointResponse.StatusCode);
    }

    [Fact]
    public async Task AdminRegistration_CreatesSalonAndAdminUserTransactionally()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var req = new RegisterAdminRequest
        {
            SalonName = "Elite Barber " + unique,
            BusinessType = "Barbershop",
            FullName = "Barber Boss " + unique,
            Email = $"boss_{unique}@elitebarber.com",
            Password = "AdminSecurePassword123!"
        };

        var response = await _client.PostAsJsonAsync("/api/auth/admin/register", req);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(result);
        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
        Assert.Equal(Roles.Admin, result.User.Role);
        Assert.NotNull(result.User.SalonId);
        Assert.NotNull(result.User.SalonSlug);
        Assert.Contains("elite-barber", result.User.SalonSlug);

        // Duplicate registration fails with 409 Conflict
        var dupResponse = await _client.PostAsJsonAsync("/api/auth/admin/register", req);
        Assert.Equal(HttpStatusCode.Conflict, dupResponse.StatusCode);
    }

    [Fact]
    public async Task CustomerRegistrationAndLogin_AssignsUserRoleAndAllowsGlobalAccess()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var req = new RegisterUserRequest
        {
            FullName = "Jane Customer",
            Email = $"jane_{unique}@customer.com",
            Password = "CustomerSecurePassword123!"
        };

        var regResponse = await _client.PostAsJsonAsync("/api/auth/user/register", req);
        Assert.Equal(HttpStatusCode.Created, regResponse.StatusCode);

        var regResult = await regResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(regResult);
        Assert.Equal(Roles.User, regResult.User.Role);
        Assert.Null(regResult.User.SalonId); // Customer is global

        // Login as customer
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/user/login", new LoginRequest
        {
            Email = req.Email,
            Password = req.Password
        });

        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        // Customer cannot login as admin
        var adminLoginResponse = await _client.PostAsJsonAsync("/api/auth/admin/login", new LoginRequest
        {
            Email = req.Email,
            Password = req.Password
        });
        Assert.Equal(HttpStatusCode.Unauthorized, adminLoginResponse.StatusCode);
    }

    [Fact]
    public async Task RefreshTokenRotation_And_ReuseDetection_WorksAsExpected()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var req = new RegisterUserRequest
        {
            FullName = "Refresh Tester",
            Email = $"refresh_{unique}@test.com",
            Password = "Password123!"
        };

        var regResponse = await _client.PostAsJsonAsync("/api/auth/user/register", req);
        var auth1 = await regResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth1);

        // First refresh: Rotates token
        var refreshResponse1 = await _client.PostAsJsonAsync("/api/auth/user/refresh", new RefreshTokenRequest
        {
            RefreshToken = auth1.RefreshToken
        });

        Assert.Equal(HttpStatusCode.OK, refreshResponse1.StatusCode);
        var auth2 = await refreshResponse1.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth2);
        Assert.NotEqual(auth1.RefreshToken, auth2.RefreshToken);

        // Reuse old refresh token (auth1.RefreshToken) -> Token Reuse Detected!
        var reuseResponse = await _client.PostAsJsonAsync("/api/auth/user/refresh", new RefreshTokenRequest
        {
            RefreshToken = auth1.RefreshToken
        });

        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);

        // Since token family is revoked, auth2.RefreshToken should now also be rejected
        var familyRevokedResponse = await _client.PostAsJsonAsync("/api/auth/user/refresh", new RefreshTokenRequest
        {
            RefreshToken = auth2.RefreshToken
        });

        Assert.Equal(HttpStatusCode.Unauthorized, familyRevokedResponse.StatusCode);
    }

    [Fact]
    public async Task TenantIsolation_RestrictsAdminToOwnSalonData()
    {
        using var clientA = _factory.CreateClient();
        using var clientB = _factory.CreateClient();

        // 1. Register Salon A Admin
        var uniqueA = Guid.NewGuid().ToString("N")[..8];
        var regA = await clientA.PostAsJsonAsync("/api/auth/admin/register", new RegisterAdminRequest
        {
            SalonName = "Salon A " + uniqueA,
            BusinessType = "Hair",
            FullName = "Admin A",
            Email = $"admin_a_{uniqueA}@salona.com",
            Password = "Password123!"
        });
        var authA = await regA.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authA);
        var salonAId = authA.User.SalonId!.Value;

        // 2. Register Salon B Admin
        var uniqueB = Guid.NewGuid().ToString("N")[..8];
        var regB = await clientB.PostAsJsonAsync("/api/auth/admin/register", new RegisterAdminRequest
        {
            SalonName = "Salon B " + uniqueB,
            BusinessType = "Hair",
            FullName = "Admin B",
            Email = $"admin_b_{uniqueB}@salonb.com",
            Password = "Password123!"
        });
        var authB = await regB.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(authB);
        var salonBId = authB.User.SalonId!.Value;

        // Admin A accesses Salon A data -> 200 OK
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authA.AccessToken);
        var ownSalonResponse = await clientA.GetAsync($"/api/test/salons/{salonAId}/protected-data");
        Assert.Equal(HttpStatusCode.OK, ownSalonResponse.StatusCode);

        // Admin A attempts to access Salon B data -> 403 Forbidden!
        var crossSalonResponse = await clientA.GetAsync($"/api/test/salons/{salonBId}/protected-data");
        Assert.Equal(HttpStatusCode.Forbidden, crossSalonResponse.StatusCode);
    }

    [Fact]
    public async Task RolePolicies_EnforceProperAuthorization()
    {
        using var client = _factory.CreateClient();

        // Unauthenticated request -> 401 Unauthorized
        client.DefaultRequestHeaders.Authorization = null;
        var unauthResponse = await client.GetAsync("/api/test/superadmin-only");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthResponse.StatusCode);

        // Customer token -> 403 on SuperAdmin endpoint, 403 on SalonAdmin endpoint, 200 on Customer endpoint
        var unique = Guid.NewGuid().ToString("N")[..8];
        var custReg = await client.PostAsJsonAsync("/api/auth/user/register", new RegisterUserRequest
        {
            FullName = "Cust Policy",
            Email = $"cust_policy_{unique}@test.com",
            Password = "Password123!"
        });
        var custAuth = await custReg.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(custAuth);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", custAuth.AccessToken);

        var custOnSa = await client.GetAsync("/api/test/superadmin-only");
        Assert.Equal(HttpStatusCode.Forbidden, custOnSa.StatusCode);

        var custOnAdmin = await client.GetAsync("/api/test/salonadmin-only");
        Assert.Equal(HttpStatusCode.Forbidden, custOnAdmin.StatusCode);

        var custOnCust = await client.GetAsync("/api/test/customer-only");
        Assert.Equal(HttpStatusCode.OK, custOnCust.StatusCode);
    }

    [Fact]
    public async Task PasswordChangeAndReset_WorksSecurely()
    {
        using var client = _factory.CreateClient();
        var unique = Guid.NewGuid().ToString("N")[..8];
        var email = $"pwd_reset_{unique}@test.com";
        var oldPassword = "OldPassword123!";
        var newPassword = "NewPassword123!";

        var reg = await client.PostAsJsonAsync("/api/auth/user/register", new RegisterUserRequest
        {
            FullName = "Password Reset User",
            Email = email,
            Password = oldPassword
        });
        var auth = await reg.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);

        // 1. Forgot password request (in dev environment, devResetToken is provided for automated testing)
        var forgotResponse = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest
        {
            Email = email
        });
        Assert.Equal(HttpStatusCode.OK, forgotResponse.StatusCode);
        var forgotResult = await forgotResponse.Content.ReadFromJsonAsync<ForgotPasswordResponse>();
        Assert.NotNull(forgotResult);
        Assert.NotNull(forgotResult.DevResetToken);

        // 2. Reset password using token
        var resetResponse = await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest
        {
            Email = email,
            ResetToken = forgotResult.DevResetToken,
            NewPassword = newPassword
        });
        Assert.Equal(HttpStatusCode.OK, resetResponse.StatusCode);

        // 3. Old password no longer works
        var oldLogin = await client.PostAsJsonAsync("/api/auth/user/login", new LoginRequest
        {
            Email = email,
            Password = oldPassword
        });
        Assert.Equal(HttpStatusCode.Unauthorized, oldLogin.StatusCode);

        // 4. New password works
        var newLogin = await client.PostAsJsonAsync("/api/auth/user/login", new LoginRequest
        {
            Email = email,
            Password = newPassword
        });
        Assert.Equal(HttpStatusCode.OK, newLogin.StatusCode);
    }
}
