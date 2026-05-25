namespace ArtisanApi.Models;

public sealed class ChatPartnerDto
{
    public string UserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? ProviderProfileId { get; set; }
    public string LastMessagePreview { get; set; } = "";
    public DateTimeOffset LastMessageAt { get; set; }
    public int UnreadCount { get; set; }
}

public sealed class MessageAttachmentItemDto
{
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "application/octet-stream";
    public long SizeBytes { get; set; }
}

public sealed class MessageItemDto
{
    public Guid Id { get; set; }
    public string SenderUserId { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTimeOffset SentAt { get; set; }
    public bool IsMine { get; set; }
    public MessageAttachmentItemDto? Attachment { get; set; }
}
