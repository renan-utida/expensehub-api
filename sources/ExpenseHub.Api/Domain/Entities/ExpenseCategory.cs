namespace ExpenseHub.Api.Domain.Entities;

/// <summary>
/// Minimal category entity. It has no endpoint, seed or link to <see cref="Expense"/>.
/// </summary>
public class ExpenseCategory
{
    /// <summary>Gets or sets the identifier.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the category name.</summary>
    public string Name { get; set; } = string.Empty;
}
