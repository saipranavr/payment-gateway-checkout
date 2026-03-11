using PaymentGateway.Api.Domain;

namespace PaymentGateway.Api.Repositories;

public interface IPaymentsRepository
{
    void Add(Payment payment);
    Payment? Get(Guid id);
}
