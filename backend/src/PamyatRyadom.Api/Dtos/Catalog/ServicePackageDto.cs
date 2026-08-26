namespace PamyatRyadom.Api.Dtos.Catalog;

/// <summary>
/// A package as the catalogue shows it.
///
/// `Limits` is not an afterthought field: the product rule is that a limitation is visible before
/// payment and never hidden behind a tooltip, so it travels with the price rather than being
/// something a client discovers on the order page.
/// </summary>
public sealed class ServicePackageDto
{
    public string Code { get; init; } = string.Empty;

    /// <summary>The version an order created now would be bound to.</summary>
    public string Version { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<string> Includes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Limits { get; init; } = Array.Empty<string>();

    /// <summary>Entry price in roubles. Decimal, never float — see CONVENTIONS.md.</summary>
    public decimal PriceFromRub { get; init; }

    public int WarrantyDays { get; init; }
    public string? VisitsLabel { get; init; }
}
