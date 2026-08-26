namespace PamyatRyadom.Api.Dtos.Catalog;

/// <summary>A package as the admin sees it — every version, every status, plus the checklist.</summary>
public sealed class AdminPackageDto
{
    public long Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<string> Includes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Limits { get; init; } = Array.Empty<string>();
    public decimal PriceFromRub { get; init; }
    public int WarrantyDays { get; init; }
    public string? VisitsLabel { get; init; }
    public int SortOrder { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset? PublishedAt { get; init; }

    /// <summary>False once published. The editor uses it to show the form read-only rather than
    /// letting someone type into fields the server will refuse to save.</summary>
    public bool Editable { get; init; }

    public IReadOnlyList<SaveChecklistItemDto> ChecklistItems { get; init; } = Array.Empty<SaveChecklistItemDto>();
    public IReadOnlyList<SaveRequiredMediaDto> RequiredMedia { get; init; } = Array.Empty<SaveRequiredMediaDto>();
}

public sealed class SavePackageDto
{
    public string Code { get; init; } = string.Empty;
    public string Version { get; init; } = "1.0";
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public IReadOnlyList<string>? Includes { get; init; }
    public IReadOnlyList<string>? Limits { get; init; }
    public decimal PriceFromRub { get; init; }
    public int WarrantyDays { get; init; }
    public string? VisitsLabel { get; init; }
    public int SortOrder { get; init; }
    public List<SaveChecklistItemDto>? ChecklistItems { get; init; }
    public List<SaveRequiredMediaDto>? RequiredMedia { get; init; }
}

public sealed class SaveChecklistItemDto
{
    /// <summary>Stable key. Left empty, one is derived from the title — but an existing key must
    /// be kept, because visit results reference it.</summary>
    public string? Key { get; init; }

    public string Title { get; init; } = string.Empty;

    /// <summary>An optional item may be skipped without a reason; a required one may not.</summary>
    public bool Optional { get; init; }
}

public sealed class SaveRequiredMediaDto
{
    /// <summary>"before", "process" or "after".</summary>
    public string Phase { get; init; } = string.Empty;
    public int MinCount { get; init; }
    public string? Description { get; init; }
}

public sealed class AdminPlanDto
{
    public long Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string ServicePackageCode { get; init; } = string.Empty;
    public int VisitsTotal { get; init; }
    public int PeriodMonths { get; init; }
    public decimal PriceRub { get; init; }
    public decimal PricePerVisit { get; init; }
    public int SortOrder { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTimeOffset? PublishedAt { get; init; }
    public bool Editable { get; init; }
}

public sealed class SavePlanDto
{
    public string Code { get; init; } = string.Empty;
    public string Version { get; init; } = "1.0";
    public string Title { get; init; } = string.Empty;
    public string? Summary { get; init; }
    public string ServicePackageCode { get; init; } = string.Empty;
    public int VisitsTotal { get; init; }
    public int PeriodMonths { get; init; }
    public decimal PriceRub { get; init; }
    public int SortOrder { get; init; }
}

public sealed class NewVersionDto
{
    public string Version { get; init; } = string.Empty;
}
