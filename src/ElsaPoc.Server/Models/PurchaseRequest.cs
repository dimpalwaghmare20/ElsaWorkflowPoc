using System.ComponentModel.DataAnnotations;

namespace ElsaPoc.Server.Models;

public class PurchaseRequest
{
    [Required, StringLength(100)]
    public string RequestedBy { get; set; } = "";

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = "";

    [Required, StringLength(100)]
    public string Site { get; set; } = "";

    [Required, StringLength(150)]
    public string Equipment { get; set; } = "";

    [Range(typeof(decimal), "0.01", "10000000")]
    public decimal EstimatedCost { get; set; }

    [Required, StringLength(500)]
    public string Justification { get; set; } = "";

    public bool IsValid() =>
        Validator.TryValidateObject(this, new ValidationContext(this), new List<ValidationResult>(), true);
}
