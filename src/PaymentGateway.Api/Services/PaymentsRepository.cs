using PaymentGateway.Api.Models.Responses;

namespace PaymentGateway.Api.Services;

public class PaymentsRepository
{
    private readonly List<PaymentResponse> _payments = new();

    public void Add(PaymentResponse payment)
    {
        _payments.Add(payment);
    }

    public PaymentResponse? Get(Guid id)
    {
        return _payments.FirstOrDefault(p => p.Id == id);
    }
}