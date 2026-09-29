using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportMate.Domain.Entities;

namespace SportMate.Infrastructure.Persistence.Configurations;

internal sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("bookings", t =>
        {
            t.HasCheckConstraint("ck_bookings_time_range", "starts_at_utc < ends_at_utc");
            t.HasCheckConstraint("ck_bookings_total_price", "total_price >= 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.StartsAtUtc).IsRequired();
        builder.Property(x => x.EndsAtUtc).IsRequired();
        builder.Property(x => x.TotalPrice).HasPrecision(10, 2).IsRequired();
        builder.Property(x => x.Currency).HasColumnType("char(3)").IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.CancellationReason).HasMaxLength(Booking.CancellationReasonMaxLength);
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        // Bookings are never hard-deleted, so every relationship restricts deletes.
        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Court)
            .WithMany(c => c.Bookings)
            .HasForeignKey(x => x.CourtId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CourtSport)
            .WithMany()
            .HasForeignKey(x => new { x.CourtSportId, x.CourtId })
            .HasPrincipalKey(cs => new { cs.Id, cs.CourtId })
            .OnDelete(DeleteBehavior.Restrict);

        // Supports the overlap query for a physical court. This index is not a concurrency
        // guard: callers must lock the courts row (SELECT ... FOR UPDATE) before checking.
        builder.HasIndex(x => new { x.CourtId, x.StartsAtUtc, x.EndsAtUtc });
        builder.HasIndex(x => new { x.UserId, x.StartsAtUtc });
    }
}
