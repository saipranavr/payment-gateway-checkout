using Microsoft.AspNetCore.Mvc;

using PaymentGateway.Api.Enums;
using PaymentGateway.Api.Models.Requests;
using PaymentGateway.Api.Models.Responses;
using PaymentGateway.Api.Services;

namespace PaymentGateway.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [HttpPost]
    public async Task<ActionResult<PaymentResponse>> PostPaymentAsync(PostPaymentRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new PaymentResponse { Status = PaymentStatus.Rejected });

        var result = await _paymentService.ProcessPaymentAsync(request);

        // Expiry cross-field check failed inside the service
        if (result.Status == PaymentStatus.Rejected)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public ActionResult<PaymentResponse> GetPaymentAsync(Guid id)
    {
        var payment = _paymentService.GetPayment(id);
        return payment is null ? NotFound() : Ok(payment);
    }
}