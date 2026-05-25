namespace ArtisanApi.Data.Entities;

public sealed class DirectMessage
{
    public Guid Id { get; set; }
    public string SenderUserId { get; set; } = "";
    public string RecipientUserId { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTimeOffset SentAt { get; set; }

    /// <summary>Stored file name under Data/chat-attachments/{Id}/ (unique, safe).</summary>
    public string? AttachmentStoredName { get; set; }

    public string? AttachmentOriginalName { get; set; }
    public string? AttachmentContentType { get; set; }
    public long? AttachmentSizeBytes { get; set; }
}
