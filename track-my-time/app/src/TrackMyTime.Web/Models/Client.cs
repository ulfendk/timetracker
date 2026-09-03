namespace TrackMyTime.Web.Models;

/// <summary>A consultancy client. Archived (IsActive = false) rather than deleted, so historic
/// time entries always resolve to a name.</summary>
public sealed class Client
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
}
