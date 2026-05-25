namespace ArtisanApi.Data.Entities;



/// <summary>Customer booking request for a provider — pending until accepted or rejected.</summary>

public sealed class ServiceRequest

{

    public Guid Id { get; set; }

    public string CustomerUserId { get; set; } = "";

    public string ProviderUserId { get; set; } = "";

    public string ProviderProfileId { get; set; } = "";

    /// <summary>pending, accepted, rejected</summary>

    public string Status { get; set; } = "pending";

    public string Body { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? RespondedAt { get; set; }

}

