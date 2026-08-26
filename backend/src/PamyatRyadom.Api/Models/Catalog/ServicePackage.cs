using System.Text.Json;

namespace PamyatRyadom.Api.Models.Catalog;

/// <summary>
/// One sellable package of work, at one version.
///
/// Versioned rather than mutable, and the reason is not tidiness: an order copies the version it
/// was sold under and lives with it to the end. Without that, the first price change would
/// rewrite the history of every past order, and a dispute about what was bought would have no
/// answer. A published version is never edited — a change means a new version (BR-017).
/// </summary>
public sealed class ServicePackage
{
    public long Id { get; set; }

    /// <summary>Stable identity across versions: "basic", "seasonal", "flowers". The code is what
    /// an order refers to conceptually; the version is what it is actually bound to.</summary>
    public string Code { get; set; } = string.Empty;

    public string Version { get; set; } = "1.0";
    public string Locale { get; set; } = "ru";

    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;

    /// <summary>What the client gets, as an ordered list of plain lines. Shown on the catalogue
    /// and copied into the order snapshot.</summary>
    public JsonDocument? Includes { get; set; }

    /// <summary>The limits that must be visible before payment — area covered, what is explicitly
    /// not included. The product rule is that a limitation is never hidden behind a tooltip, so
    /// this is a first-class field rather than a footnote.</summary>
    public JsonDocument? Limits { get; set; }

    /// <summary>Entry price. The final figure comes from the estimate, since the real cost depends
    /// on the state of the plot — which is why this is "from" and never presented as the price.</summary>
    public decimal PriceFromRub { get; set; }

    /// <summary>How long after the report a client may still raise a complaint (BR-012).</summary>
    public int WarrantyDays { get; set; }

    /// <summary>Typical number of visits, for the catalogue card ("1 визит", "1–2 визита").</summary>
    public string? VisitsLabel { get; set; }

    public int SortOrder { get; set; }

    public string Status { get; set; } = ServicePackageStatuses.Draft;
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ChecklistTemplate? ChecklistTemplate { get; set; }
}

public static class ServicePackageStatuses
{
    public const string Draft = "draft";

    /// <summary>Sellable, and from here immutable.</summary>
    public const string Published = "published";

    /// <summary>Withdrawn from sale. Orders already bound to it are unaffected — that is the
    /// point of binding to a version rather than to a code.</summary>
    public const string Archived = "archived";

    public static readonly IReadOnlyCollection<string> All = new[] { Draft, Published, Archived };
}

/// <summary>Well-known package codes. Prices and composition live in the seeded rows, not here.</summary>
public static class ServicePackageCodes
{
    public const string Inspection = "inspection";
    public const string Basic = "basic";
    public const string Seasonal = "seasonal";
    public const string Flowers = "flowers";
}
