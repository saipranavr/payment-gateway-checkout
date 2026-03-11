using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using PaymentGateway.Api.Controllers;
using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Repositories;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

public class PaymentsControllerTests
{
    private readonly Random _random = new();

    // -----------------------------------------------------------------------
    // GET /api/payments/{id}
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetPayment_ReturnsPayment_WhenFound()
    {
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            Status = PaymentStatus.Authorized,
            ExpiryYear = _random.Next(2025, 2030),
            ExpiryMonth = _random.Next(1, 12),
            Amount = _random.Next(1, 10000),
            CardNumberLastFour = _random.Next(1111, 9999),
            Currency = "GBP"
        };

        var repo = new PaymentsRepository();
        repo.Add(payment);

        var response = await BuildClient(repository: repo)
            .GetAsync($"/api/Payments/{payment.Id}");
        var body = await response.Content.ReadFromJsonAsync<PaymentResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(payment.Id, body.Id);
        Assert.Equal(payment.CardNumberLastFour, body.CardNumberLastFour);
        Assert.Equal(payment.Currency, body.Currency);
        Assert.Equal(payment.Status, body.Status);
    }

    [Fact]
    public async Task GetPayment_Returns404_WhenNotFound()
    {
        var response = await BuildClient()
            .GetAsync($"/api/Payments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // -----------------------------------------------------------------------
    // POST /api/payments — field-level validation → Rejected (400)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task PostPayment_Returns400_WhenCardNumberInvalid()
    {
        var request = ValidRequest();
        request.CardNumber = "1234"; // too short (< 14 digits)

        var response = await BuildClient().PostAsJsonAsync("/api/Payments", request);
        var body     = await response.Content.ReadFromJsonAsync<PaymentResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(PaymentStatus.Rejected, body!.Status);
    }

    [Fact]
    public async Task PostPayment_Returns400_WhenCurrencyInvalid()
    {
        var request = ValidRequest();
        request.Currency = "JPY"; // not in the allowed set (USD, GBP, EUR)

        var response = await BuildClient().PostAsJsonAsync("/api/Payments", request);
        var body     = await response.Content.ReadFromJsonAsync<PaymentResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(PaymentStatus.Rejected, body!.Status);
    }

    [Fact]
    public async Task PostPayment_Returns400_WhenCardNumberContainsLetters()
    {
        var request = ValidRequest();
        request.CardNumber = "2222405343ABCD77"; // correct length but non-numeric

        var response = await BuildClient().PostAsJsonAsync("/api/Payments", request);
        var body     = await response.Content.ReadFromJsonAsync<PaymentResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(PaymentStatus.Rejected, body!.Status);
    }

    [Fact]
    public async Task PostPayment_Returns400_WhenCvvInvalid()
    {
        var request = ValidRequest();
        request.Cvv = "12"; // too short (< 3 digits)

        var response = await BuildClient().PostAsJsonAsync("/api/Payments", request);
        var body     = await response.Content.ReadFromJsonAsync<PaymentResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(PaymentStatus.Rejected, body!.Status);
    }

    [Fact]
    public async Task PostPayment_Returns400_WhenAmountIsZero()
    {
        var request = ValidRequest();
        request.Amount = 0; // fails [Range(1, int.MaxValue)]

        var response = await BuildClient().PostAsJsonAsync("/api/Payments", request);
        var body     = await response.Content.ReadFromJsonAsync<PaymentResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(PaymentStatus.Rejected, body!.Status);
    }

    // -----------------------------------------------------------------------
    // POST /api/payments — cross-field expiry check → Rejected (400)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task PostPayment_Returns400_WhenExpiryInPast()
    {
        // Month=1, Year=2024 passes the individual [Range] annotations but is
        // in the past, so the service cross-field check must catch it.
        var request = ValidRequest();
        request.ExpiryMonth = 1;
        request.ExpiryYear  = 2024;

        var fake     = new FakeBankClient(new BankPaymentResponse(Authorized: true, AuthorizationCode: "unused"));
        var response = await BuildClient(bankClient: fake).PostAsJsonAsync("/api/Payments", request);
        var body     = await response.Content.ReadFromJsonAsync<PaymentResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(PaymentStatus.Rejected, body!.Status);
    }

    // -----------------------------------------------------------------------
    // POST /api/payments — bank authorized / declined → 200
    // -----------------------------------------------------------------------

    [Fact]
    public async Task PostPayment_Returns200Authorized_WhenBankAuthorizes()
    {
        var fake     = new FakeBankClient(new BankPaymentResponse(Authorized: true, AuthorizationCode: "auth-code-123"));
        var response = await BuildClient(bankClient: fake).PostAsJsonAsync("/api/Payments", ValidRequest());
        var body     = await response.Content.ReadFromJsonAsync<PaymentResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(PaymentStatus.Authorized, body!.Status);
        Assert.Equal(8877, body.CardNumberLastFour); // last 4 of "2222405343248877"
        Assert.Equal("GBP", body.Currency);
        Assert.Equal(100, body.Amount);
        Assert.NotEqual(Guid.Empty, body.Id);
    }

    [Fact]
    public async Task PostPayment_Returns200Declined_WhenBankDeclines()
    {
        var fake     = new FakeBankClient(new BankPaymentResponse(Authorized: false, AuthorizationCode: null));
        var response = await BuildClient(bankClient: fake).PostAsJsonAsync("/api/Payments", ValidRequest());
        var body     = await response.Content.ReadFromJsonAsync<PaymentResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(PaymentStatus.Declined, body!.Status);
        Assert.NotEqual(Guid.Empty, body.Id);
    }

    // -----------------------------------------------------------------------
    // POST → GET round-trip (proves persistence works)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task GetPayment_ReturnsStoredPayment_AfterSuccessfulPost()
    {
        var repo   = new PaymentsRepository();
        var fake   = new FakeBankClient(new BankPaymentResponse(Authorized: true, AuthorizationCode: "code-abc"));
        var client = BuildClient(repository: repo, bankClient: fake);

        var postResponse = await client.PostAsJsonAsync("/api/Payments", ValidRequest());
        var postBody     = await postResponse.Content.ReadFromJsonAsync<PaymentResponse>();

        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);

        var getResponse = await client.GetAsync($"/api/Payments/{postBody!.Id}");
        var getBody     = await getResponse.Content.ReadFromJsonAsync<PaymentResponse>();

        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.NotNull(getBody);
        Assert.Equal(postBody.Id,                 getBody!.Id);
        Assert.Equal(postBody.Status,             getBody.Status);
        Assert.Equal(postBody.CardNumberLastFour, getBody.CardNumberLastFour);
        Assert.Equal(postBody.Currency,           getBody.Currency);
        Assert.Equal(postBody.Amount,             getBody.Amount);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static PostPaymentRequest ValidRequest() => new()
    {
        CardNumber = "2222405343248877", // 16 digits; last 4 = 8877
        ExpiryMonth = 4,
        ExpiryYear = 2027,
        Currency = "GBP",
        Amount = 100,
        Cvv = "123"
    };

    private static HttpClient BuildClient(
        IPaymentsRepository?  repository = null,
        IBankSimulatorClient? bankClient = null)
    {
        return new WebApplicationFactory<PaymentsController>()
            .WithWebHostBuilder(builder =>
                builder.ConfigureServices(services =>
                {
                    ReplaceService(services, repository ?? new PaymentsRepository());
                    if (bankClient is not null)
                        ReplaceService(services, bankClient);
                }))
            .CreateClient();
    }

    private static void ReplaceService<T>(IServiceCollection services, T instance) where T : class
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(T));
        if (descriptor is not null)
            services.Remove(descriptor);
        services.AddSingleton(instance);
    }

    private sealed class FakeBankClient : IBankSimulatorClient
    {
        private readonly BankPaymentResponse _response;
        public FakeBankClient(BankPaymentResponse response) => _response = response;

        public Task<BankPaymentResponse?> ProcessPaymentAsync(BankPaymentRequest request)
            => Task.FromResult<BankPaymentResponse?>(_response);
    }
}