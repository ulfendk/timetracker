namespace TrackMyTime.Web.Models;

/// <summary>A project belonging to a <see cref="Client"/>. Archived rather than deleted.</summary>
public sealed class Project
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Hex color (e.g. "#7E57C2") used to distinguish the project in charts. Optional.</summary>
    public string? Color { get; set; }
}

/// <summary>A <see cref="Project"/> joined with its owning client's name, for list/report views.</summary>
public sealed class ProjectWithClient
{
    public int Id { get; set; }
    public int ClientId { get; set; }
    public required string Name { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Color { get; set; }
    public required string ClientName { get; set; }
}
