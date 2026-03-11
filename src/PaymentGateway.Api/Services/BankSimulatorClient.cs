using System.Net;
using System.Net.Http.Json;

using PaymentGateway.Api.Models.Bank;

namespace PaymentGateway.Api.Services;

public class BankSimulatorClient : IBankSimulatorClient
{
    private readonly HttpClient _httpClient;

    public BankSimulatorClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<BankPaymentResponse?> ProcessPaymentAsync(BankPaymentRequest request)
    {
        var response = await _httpClient.PostAsJsonAsync("/payments", request);

        // Bank temporarily unavailable (card ending in 0)
        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
            return new BankPaymentResponse(Authorized: false, AuthorizationCode: null);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<BankPaymentResponse>();
    }
}
