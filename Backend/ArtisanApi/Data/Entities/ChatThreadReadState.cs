namespace ArtisanApi.Data.Entities;



/// <summary>Per-user read cursor for a 1:1 chat thread (incoming messages with SentAt &lt;= LastReadAt are considered read).</summary>

public sealed class ChatThreadReadState

{

    public string ReaderUserId { get; set; } = "";

    public string PartnerUserId { get; set; } = "";

    public DateTimeOffset LastReadAt { get; set; }

}

