using System.Text.Json;

namespace PamyatRyadom.Api.Models.Catalog;

/// <summary>
/// The obligatory scope of work for one package version, and the photographs that must come back
/// with it.
///
/// Bound to a package version rather than to a package code, and immutable once its package is
/// published: the checklist is the definition of "done" that QA measures a visit against, so a
/// checklist edited after the fact would move the goalposts on work already accepted.
/// </summary>
public sealed class ChecklistTemplate
{
    public long Id { get; set; }
    public long ServicePackageId { get; set; }

    /// <summary>Ordered checklist items. Each carries a stable key, a title and whether it may be
    /// skipped — an executor marks every one done / impossible / not required, and anything but
    /// "done" demands a reason (FR-EXE-009).</summary>
    public JsonDocument? Items { get; set; }

    /// <summary>Which camera angles are mandatory before and after. The minimum set is what stops
    /// an incomplete report reaching QA at all (BR-008).</summary>
    public JsonDocument? RequiredMedia { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ServicePackage? ServicePackage { get; set; }
}

/// <summary>Shape of one entry in <see cref="ChecklistTemplate.Items"/>.</summary>
public sealed record ChecklistItem(string Key, string Title, bool Optional = false);

/// <summary>Shape of one entry in <see cref="ChecklistTemplate.RequiredMedia"/>.</summary>
public sealed record RequiredMediaRule(string Phase, int MinCount, string Description);
