using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportMate.Domain.Entities;

namespace SportMate.Infrastructure.Persistence.Configurations;

internal sealed class CourtSportConfiguration : IEntityTypeConfiguration<CourtSport>
{
    public void Configure(EntityTypeBuilder<CourtSport> builder)
    {
        builder.ToTable("court_sports", t =>
            t.HasCheckConstraint("ck_court_sports_price_per_hour", "price_per_hour > 0"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.PricePerHour).HasPrecision(10, 2).IsRequired();
        builder.Property(x => x.Currency).HasColumnType("char(3)").IsRequired();
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        builder.HasOne(x => x.Court)
            .WithMany(c => c.CourtSports)
            .HasForeignKey(x => x.CourtId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Sport)
            .WithMany()
            .HasForeignKey(x => x.SportId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.CourtId, x.SportId }).IsUnique();

        // Target of the composite FK from bookings, guaranteeing bookings.court_id matches the court sport.
        builder.HasAlternateKey(x => new { x.Id, x.CourtId });
    }
}
