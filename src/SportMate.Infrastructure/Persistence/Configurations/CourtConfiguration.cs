using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportMate.Domain.Entities;

namespace SportMate.Infrastructure.Persistence.Configurations;

internal sealed class CourtConfiguration : IEntityTypeConfiguration<Court>
{
    public void Configure(EntityTypeBuilder<Court> builder)
    {
        builder.ToTable("courts", t =>
        {
            t.HasCheckConstraint("ck_courts_latitude", "latitude BETWEEN -90 AND 90");
            t.HasCheckConstraint("ck_courts_longitude", "longitude BETWEEN -180 AND 180");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Name).HasMaxLength(Court.NameMaxLength).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(Court.DescriptionMaxLength);
        builder.Property(x => x.Address).HasMaxLength(Court.AddressMaxLength).IsRequired();
        builder.Property(x => x.City).HasMaxLength(Court.CityMaxLength).IsRequired();
        builder.Property(x => x.Latitude).HasPrecision(9, 6).IsRequired();
        builder.Property(x => x.Longitude).HasPrecision(9, 6).IsRequired();
        builder.Property(x => x.TimeZoneId).HasMaxLength(Court.TimeZoneIdMaxLength).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        // The owner is a user with the CourtOwner role (checked by the application); there is no
        // separate owner table.
        builder.HasOne(x => x.Owner)
            .WithMany()
            .HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.OwnerId);
        builder.HasIndex(x => new { x.Latitude, x.Longitude });
    }
}
