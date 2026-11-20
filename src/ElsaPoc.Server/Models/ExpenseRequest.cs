namespace ElsaPoc.Server.Models;

public class ExpenseRequest
{
    public string Employee { get; set; } = "";
    public string Email { get; set; } = "";
    public decimal Amount { get; set; }
    public string Description { get; set; } = "";
}
