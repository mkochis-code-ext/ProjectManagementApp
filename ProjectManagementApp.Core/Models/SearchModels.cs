namespace ProjectManagementApp.Models;

/// <summary>A lane matched by a name search, carrying its owning board's context.</summary>
public record LaneSearchResult(Guid BoardId, string BoardName, Lane Lane);

/// <summary>A card matched by a title search, carrying its board and lane context
/// so callers can update, move, or delete it without a separate lookup.</summary>
public record CardSearchResult(Guid BoardId, string BoardName, Guid LaneId, string LaneName, Card Card);

/// <summary>A todo matched by a text search. Board-level todos have null lane/card
/// context; card-level todos include their lane and card.</summary>
public record TodoSearchResult(
    Guid BoardId,
    string BoardName,
    Guid? LaneId,
    string? LaneName,
    Guid? CardId,
    string? CardTitle,
    TodoItem Todo);
