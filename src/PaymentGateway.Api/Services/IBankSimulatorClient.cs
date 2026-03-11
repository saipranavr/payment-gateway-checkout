namespace PaymentGateway.Api.Services;

public interface IBankSimulatorClient
{
    Task<Models.Bank.BankPaymentResponse?> ProcessPaymentAsync(Models.Bank.BankPaymentRequest request);
}
