using System.ComponentModel.DataAnnotations;

namespace PaymentGateway.Api.Models.Requests;

public class PostPaymentRequest
{
    [Required]
    [StringLength(19, MinimumLength = 14)]
    [RegularExpression(@"^\d+$", ErrorMessage = "Card number must contain only numeric characters.")]
    public string CardNumber { get; set; } = string.Empty;

    [Required]
    [Range(1, 12)]
    public int ExpiryMonth { get; set; }

    [Required]
    [Range(2024, 9999)]
    public int ExpiryYear { get; set; }

    [Required]
    [RegularExpression(@"^(USD|GBP|EUR)$", ErrorMessage = "Currency must be USD, GBP, or EUR.")]
    public string Currency { get; set; } = string.Empty;

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Amount must be a positive integer.")]
    public int Amount { get; set; }

    [Required]
    [RegularExpression(@"^\d{3,4}$", ErrorMessage = "CVV must be 3 or 4 numeric characters.")]
    public string Cvv { get; set; } = string.Empty;
}