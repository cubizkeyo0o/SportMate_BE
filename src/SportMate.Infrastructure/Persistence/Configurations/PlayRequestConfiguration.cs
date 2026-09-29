using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportMate.Domain.Entities;

namespace SportMate.Infrastructure.Persistence.Configurations;

internal sealed class PlayRequestConfiguration : IEntityTypeConfiguration<PlayRequest>
{
    public void Configure(EntityTypeBuilder<PlayRequest> builder)
    {
        builder.ToTable("play_requests", t =>
        {
            t.HasCheckConstraint("ck_play_requests_time_range", "starts_at_utc < ends_at_utc");
            t.HasCheckConstraint("ck_play_requests_spots_needed", "spots_needed > 0");
            t.HasCheckConstraint(
                "ck_play_requests_skill_range",
                "min_skill_level IS NULL OR max_skill_level IS NULL OR min_skill_level <= max_skill_level");
            t.HasCheckConstraint("ck_play_requests_latitude", "latitude BETWEEN -90 AND 90");
            t.HasCheckConstraint("ck_play_requests_longitude", "longitude BETWEEN -180 AND 180");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Title).HasMaxLength(PlayRequest.TitleMaxLength).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(PlayRequest.DescriptionMaxLength);
        builder.Property(x => x.StartsAtUtc).IsRequired();
        builder.Property(x => x.EndsAtUtc).IsRequired();
        builder.Property(x => x.SpotsNeeded).IsRequired();
        builder.Property(x => x.LocationName).HasMaxLength(PlayRequest.LocationNameMaxLength);
        builder.Property(x => x.Latitude).HasPrecision(9, 6).IsRequired();
        builder.Property(x => x.Longitude).HasPrecision(9, 6).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
        builder.Ignore(x => x.AcceptedCount);

        // Play requests are never hard-deleted.
        builder.HasOne(x => x.Creator)
            .WithMany()
            .HasForeignKey(x => x.CreatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Sport)
            .WithMany()
            .HasForeignKey(x => x.SportId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.SportId, x.Status, x.StartsAtUtc });
        builder.HasIndex(x => new { x.CreatorId, x.StartsAtUtc });
        builder.HasIndex(x => new { x.Latitude, x.Longitude });
    }
}
