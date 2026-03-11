using PaymentGateway.Api.Domain;
using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Models.Bank;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Repositories;

namespace PaymentGateway.Api.Services;

public class PaymentService : IPaymentService
{
    private readonly IBankSimulatorClient _bankClient;
    private readonly IPaymentsRepository _repository;

    public PaymentService(IBankSimulatorClient bankClient, IPaymentsRepository repository)
    {
        _bankClient = bankClient;
        _repository = repository;
    }

    public async Task<PaymentResponse> ProcessPaymentAsync(PostPaymentRequest request)
    {
        var expiryDate    = new DateOnly(request.ExpiryYear, request.ExpiryMonth, 1);
        var firstOfMonth  = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);

        if (expiryDate < firstOfMonth)
            return new PaymentResponse { Status = PaymentStatus.Rejected };

        var bankRequest = new BankPaymentRequest(
            CardNumber: request.CardNumber,
            ExpiryDate: $"{request.ExpiryMonth:D2}/{request.ExpiryYear}",
            Currency: request.Currency,
            Amount: request.Amount,
            Cvv: request.Cvv);

        var bankResponse = await _bankClient.ProcessPaymentAsync(bankRequest);

        var status = bankResponse?.Authorized == true
            ? PaymentStatus.Authorized
            : PaymentStatus.Declined;

        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            Status = status,
            CardNumberLastFour = int.Parse(request.CardNumber[^4..]),
            ExpiryMonth = request.ExpiryMonth,
            ExpiryYear = request.ExpiryYear,
            Currency = request.Currency,
            Amount = request.Amount
        };

        _repository.Add(payment);

        return ToResponse(payment);
    }

    public PaymentResponse? GetPayment(Guid id)
    {
        var payment = _repository.Get(id);
        return payment is null ? null : ToResponse(payment);
    }

    private static PaymentResponse ToResponse(Payment payment) => new()
    {
        Id = payment.Id,
        Status = payment.Status,
        CardNumberLastFour = payment.CardNumberLastFour,
        ExpiryMonth = payment.ExpiryMonth,
        ExpiryYear = payment.ExpiryYear,
        Currency = payment.Currency,
        Amount = payment.Amount
    };
}
