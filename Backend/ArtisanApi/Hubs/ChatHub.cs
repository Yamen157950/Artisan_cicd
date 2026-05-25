using System.Security.Claims;
using ArtisanApi.Data;
using ArtisanApi.Data.Entities;
using ArtisanApi.Models;
using ArtisanApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;

namespace ArtisanApi.Hubs;

/// <summary>
/// Real-time direct messages. Each user only receives pushes for threads they participate in (JWT user id).
/// </summary>
[Authorize]
public sealed class ChatHub : Hub
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _users;

    public ChatHub(AppDbContext db, UserManager<ApplicationUser> users)
    {
        _db = db;
        _users = users;
    }

    /// <summary>Optional: reserved for future typing indicators; clients may call on open chat.</summary>
    public Task JoinThread(string partnerUserId) => Task.CompletedTask;

    public Task LeaveThread() => Task.CompletedTask;

    public async Task SendChatMessage(string partnerUserId, string body)
    {
        var me = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(me))
            throw new HubException("Not authenticated.");

        if (string.IsNullOrWhiteSpace(partnerUserId))
            throw new HubException("Invalid recipient.");

        partnerUserId = partnerUserId.Trim();
        if (partnerUserId == me)
            throw new HubException("Cannot message yourself.");

        if (await _users.FindByIdAsync(partnerUserId) is null)
            throw new HubException("User not found.");

        var text = (body ?? "").Trim();
        if (text.Length is < 1 or > 4000)
            throw new HubException("Message must be 1–4000 characters.");

        if (ChatMessageFilters.IsHiddenAutoChatMessage(text))
            throw new HubException("Message not allowed.");

        var msg = new DirectMessage
        {
            Id = Guid.NewGuid(),
            SenderUserId = me,
            RecipientUserId = partnerUserId,
            Body = text,
            SentAt = DateTimeOffset.UtcNow,
        };
        _db.DirectMessages.Add(msg);
        await _db.SaveChangesAsync();

        object? attachment =
            string.IsNullOrEmpty(msg.AttachmentStoredName)
                ? null
                : new
                {
                    fileName = msg.AttachmentOriginalName,
                    contentType = msg.AttachmentContentType,
                    sizeBytes = msg.AttachmentSizeBytes ?? 0,
                };

        var payload = new
        {
            id = msg.Id.ToString(),
            senderUserId = me,
            recipientUserId = partnerUserId,
            body = msg.Body,
            sentAt = msg.SentAt,
            attachment,
        };

        await Clients.Users(me, partnerUserId).SendAsync("ReceiveMessage", payload);
    }
}
