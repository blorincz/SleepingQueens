namespace SleepingQueens.Shared.Models.DTOs;

public class ValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }

    public static ValidationResult Valid() => new() { IsValid = true };
    public static ValidationResult Invalid(string errorMessage)
        => new() { IsValid = false, ErrorMessage = errorMessage };
}
