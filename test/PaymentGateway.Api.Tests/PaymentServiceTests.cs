using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Repositories;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Tests;

public class PaymentServiceTests
{
    // -----------------------------------------------------------------------
    // ProcessPaymentAsync — expiry validation (service owns this rule)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ProcessPayment_ReturnsRejected_WhenExpiryInPast()
    {
        var service = BuildService();
        var request = ValidRequest();
        request.ExpiryMonth = 1;
        request.ExpiryYear = 2024; // clearly in the past

        var result = await service.ProcessPaymentAsync(request);

        Assert.Equal(PaymentStatus.Rejected, result.Status);
    }

    [Fact]
    public async Task ProcessPayment_DoesNotCallBank_WhenExpiryInPast()
    {
        var bankClient = new SpyBankClient(new BankPaymentResponse(Authorized: true, AuthorizationCode: "x"));
        var service = new PaymentService(bankClient, new PaymentsRepository());
        var request = ValidRequest();
        request.ExpiryMonth = 1;
        request.ExpiryYear = 2024;

        await service.ProcessPaymentAsync(request);

        Assert.Equal(0, bankClient.CallCount);
    }

    // -----------------------------------------------------------------------
    // ProcessPaymentAsync — bank outcome mapping
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ProcessPayment_ReturnsAuthorized_WhenBankAuthorizes()
    {
        var result = await BuildService(authorized: true).ProcessPaymentAsync(ValidRequest());

        Assert.Equal(PaymentStatus.Authorized, result.Status);
        Assert.NotEqual(Guid.Empty, result.Id);
    }

    [Fact]
    public async Task ProcessPayment_ReturnsDeclined_WhenBankDeclines()
    {
        var result = await BuildService(authorized: false).ProcessPaymentAsync(ValidRequest());

        Assert.Equal(PaymentStatus.Declined, result.Status);
        Assert.NotEqual(Guid.Empty, result.Id);
    }

    // -----------------------------------------------------------------------
    // ProcessPaymentAsync — card masking (PCI compliance)
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ProcessPayment_MasksCardToLastFourDigits()
    {
        var request = ValidRequest();
        request.CardNumber = "2222405343248877"; // last 4 = 8877

        var result = await BuildService().ProcessPaymentAsync(request);

        Assert.Equal(8877, result.CardNumberLastFour);
    }

    // -----------------------------------------------------------------------
    // ProcessPaymentAsync — persistence
    // -----------------------------------------------------------------------

    [Fact]
    public async Task ProcessPayment_StoresPaymentInRepository()
    {
        var repo    = new PaymentsRepository();
        var service = BuildService(repository: repo);

        var result  = await service.ProcessPaymentAsync(ValidRequest());
        var stored  = repo.Get(result.Id);

        Assert.NotNull(stored);
        Assert.Equal(result.Id, stored.Id);
        Assert.Equal(result.Status, stored.Status);
    }

    // -----------------------------------------------------------------------
    // GetPayment
    // -----------------------------------------------------------------------

    [Fact]
    public void GetPayment_ReturnsNull_WhenNotFound()
    {
        var result = BuildService().GetPayment(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public void GetPayment_MapsEntityToResponse_WhenFound()
    {
        var repo = new PaymentsRepository();
        var payment = new Payment
        {
            Id                 = Guid.NewGuid(),
            Status             = PaymentStatus.Authorized,
            CardNumberLastFour = 1234,
            ExpiryMonth        = 4,
            ExpiryYear         = 2027,
            Currency           = "GBP",
            Amount             = 100
        };
        repo.Add(payment);

        var result = BuildService(repository: repo).GetPayment(payment.Id);

        Assert.NotNull(result);
        Assert.Equal(payment.Id,                 result!.Id);
        Assert.Equal(PaymentStatus.Authorized,   result.Status);
        Assert.Equal(1234,                       result.CardNumberLastFour);
        Assert.Equal("GBP",                      result.Currency);
        Assert.Equal(100,                        result.Amount);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static PostPaymentRequest ValidRequest() => new()
    {
        CardNumber = "2222405343248877",
        ExpiryMonth = 4,
        ExpiryYear = 2027,
        Currency = "GBP",
        Amount = 100,
        Cvv = "123"
    };

    private static IPaymentService BuildService(
        bool authorized = true,
        IPaymentsRepository? repository = null)
    {
        var bank = new SpyBankClient(
            new BankPaymentResponse(Authorized: authorized, AuthorizationCode: authorized ? "code" : null));
        return new PaymentService(bank, repository ?? new PaymentsRepository());
    }

    private sealed class SpyBankClient : IBankSimulatorClient
    {
        private readonly BankPaymentResponse _response;
        public int CallCount { get; private set; }

        public SpyBankClient(BankPaymentResponse response) => _response = response;

        public Task<BankPaymentResponse?> ProcessPaymentAsync(BankPaymentRequest request)
        {
            CallCount++;
            return Task.FromResult<BankPaymentResponse?>(_response);
        }
    }
}
