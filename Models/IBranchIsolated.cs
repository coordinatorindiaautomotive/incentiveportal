namespace IncentivePortal.Models;

public interface IBranchIsolated
{
    /// <summary>
    /// Code of the Branch this entity belongs to.
    /// Used by EF Core Global Query Filters for cross-branch isolation.
    /// </summary>
    string SourceLocation { get; }
}
