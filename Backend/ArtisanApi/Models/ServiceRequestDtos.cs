using System.ComponentModel.DataAnnotations;



namespace ArtisanApi.Models;



public sealed class CreateServiceRequestDto

{

    [Required, MinLength(1), MaxLength(4000)]

    public string Body { get; set; } = "";



    [Required, MinLength(1), MaxLength(128)]

    public string ProviderProfileId { get; set; } = "";

}



public sealed class ServiceRequestDto

{

    public string Id { get; set; } = "";

    public string CustomerUserId { get; set; } = "";

    public string CustomerName { get; set; } = "";

    public string Body { get; set; } = "";

    public string Status { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? RespondedAt { get; set; }

}



public sealed class ServiceRequestStatusDto

{

    /// <summary>accepted or rejected</summary>

    [Required]

    public string Status { get; set; } = "";

}

/// <summary>Customer-facing rows when a provider has accepted or rejected a booking.</summary>
public sealed class CustomerBookingNotificationDto
{
    public string Id { get; set; } = "";

    public string Status { get; set; } = "";

    public string ProviderDisplayName { get; set; } = "";

    public string ProviderProfileId { get; set; } = "";

    public string ProviderUserId { get; set; } = "";

    public string Body { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? RespondedAt { get; set; }
}

