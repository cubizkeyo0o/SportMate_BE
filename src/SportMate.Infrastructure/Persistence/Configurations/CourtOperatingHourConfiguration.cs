using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportMate.Domain.Entities;

namespace SportMate.Infrastructure.Persistence.Configurations;

internal sealed class CourtOperatingHourConfiguration : IEntityTypeConfiguration<CourtOperatingHour>
{
    public void Configure(EntityTypeBuilder<CourtOperatingHour> builder)
    {
        builder.ToTable("court_operating_hours", t =>
        {
            t.HasCheckConstraint("ck_court_operating_hours_day_of_week", "day_of_week BETWEEN 0 AND 6");
            t.HasCheckConstraint("ck_court_operating_hours_time_range", "opens_at < closes_at");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        // System.DayOfWeek: Sunday = 0 ... Saturday = 6.
        builder.Property(x => x.DayOfWeek).HasConversion<byte>().IsRequired();
        builder.Property(x => x.OpensAt).IsRequired();
        builder.Property(x => x.ClosesAt).IsRequired();

        // Operating hours are configuration of the court, not history.
        builder.HasOne(x => x.Court)
            .WithMany(c => c.OperatingHours)
            .HasForeignKey(x => x.CourtId)
            .OnDelete(DeleteBehavior.Cascade);

        // Several periods per day are allowed; overlap between them is validated in the application.
        builder.HasIndex(x => new { x.CourtId, x.DayOfWeek, x.OpensAt });
    }
}
