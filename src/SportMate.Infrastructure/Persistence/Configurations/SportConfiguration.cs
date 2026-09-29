using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportMate.Domain.Entities;
using SportMate.Infrastructure.Persistence.Seed;

namespace SportMate.Infrastructure.Persistence.Configurations;

internal sealed class SportConfiguration : IEntityTypeConfiguration<Sport>
{
    public void Configure(EntityTypeBuilder<Sport> builder)
    {
        builder.ToTable("sports");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Name)
            .HasMaxLength(Sport.NameMaxLength)
            .UseCollation("utf8mb4_0900_ai_ci")
            .IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();

        builder.Property(x => x.Slug).HasMaxLength(Sport.SlugMaxLength).IsRequired();
        builder.HasIndex(x => x.Slug).IsUnique();

        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        builder.HasData(SportSeed.Sports.Select(s => new
        {
            s.Id,
            s.Name,
            s.Slug,
            IsActive = true,
            CreatedAtUtc = SportSeed.SeededAtUtc,
            UpdatedAtUtc = SportSeed.SeededAtUtc
        }));
    }
}
