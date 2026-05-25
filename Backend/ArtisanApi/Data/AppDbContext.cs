using ArtisanApi.Data.Entities;
using ArtisanApi.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ArtisanApi.Data;

public sealed class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    public DbSet<ProviderProfile> ProviderProfiles => Set<ProviderProfile>();
    public DbSet<ProviderRating> ProviderRatings => Set<ProviderRating>();
    public DbSet<DirectMessage> DirectMessages => Set<DirectMessage>();
    public DbSet<ChatThreadReadState> ChatThreadReadStates => Set<ChatThreadReadState>();
    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();
    public DbSet<ForgotPasswordChallenge> ForgotPasswordChallenges => Set<ForgotPasswordChallenge>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var p = modelBuilder.Entity<ProviderProfile>();
        p.HasKey(x => x.Id);
        p.Property(x => x.Id).HasMaxLength(128);
        p.HasIndex(x => x.UserId).IsUnique();
        p.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ProviderRating>(r =>
        {
            r.HasKey(x => x.Id);
            r.HasOne(x => x.ProviderProfile)
                .WithMany()
                .HasForeignKey(x => x.ProviderProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            r.Property(x => x.ProviderProfileId).HasMaxLength(128);
        });

        modelBuilder.Entity<ChatThreadReadState>(s =>
        {
            s.HasKey(x => new { x.ReaderUserId, x.PartnerUserId });
            s.Property(x => x.ReaderUserId).HasMaxLength(450);
            s.Property(x => x.PartnerUserId).HasMaxLength(450);
            s.HasIndex(x => x.ReaderUserId);
        });

        modelBuilder.Entity<DirectMessage>(m =>
        {
            m.HasKey(x => x.Id);
            m.Property(x => x.Body).HasMaxLength(4000);
            m.Property(x => x.AttachmentStoredName).HasMaxLength(256);
            m.Property(x => x.AttachmentOriginalName).HasMaxLength(260);
            m.Property(x => x.AttachmentContentType).HasMaxLength(128);
            m.HasIndex(x => new { x.SenderUserId, x.SentAt });
            m.HasIndex(x => new { x.RecipientUserId, x.SentAt });
        });

        modelBuilder.Entity<ServiceRequest>(s =>
        {
            s.HasKey(x => x.Id);
            s.Property(x => x.Body).HasMaxLength(4000);
            s.Property(x => x.Status).HasMaxLength(32);
            s.Property(x => x.ProviderProfileId).HasMaxLength(128);
            s.HasIndex(x => x.ProviderProfileId);
            s.HasIndex(x => new { x.ProviderProfileId, x.Status });
        });

        modelBuilder.Entity<ForgotPasswordChallenge>(c =>
        {
            c.HasKey(x => x.Id);
            c.Property(x => x.EmailNormalized).HasMaxLength(256);
            c.Property(x => x.OtpCode).HasMaxLength(16);
            c.HasIndex(x => x.EmailNormalized);
        });
    }
}
