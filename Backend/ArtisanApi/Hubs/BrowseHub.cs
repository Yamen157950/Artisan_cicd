using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ArtisanApi.Hubs;

/// <summary>
/// Anonymous feed for customer browse pages — provider visibility updates without refresh.
/// </summary>
[AllowAnonymous]
public sealed class BrowseHub : Hub
{
    public const string FeedGroup = "provider-browse-feed";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, FeedGroup);
        await base.OnConnectedAsync();
    }
}
