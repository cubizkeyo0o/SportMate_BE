using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportMate.Domain.Entities;

namespace SportMate.Infrastructure.Persistence.Configurations;

internal sealed class UserSportProfileConfiguration : IEntityTypeConfiguration<UserSportProfile>
{
    public void Configure(EntityTypeBuilder<UserSportProfile> builder)
    {
        builder.ToTable("user_sport_profiles", t =>
            t.HasCheckConstraint("ck_user_sport_profiles_skill_level", "skill_level BETWEEN 1 AND 5"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.SkillLevel).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        builder.HasOne(x => x.User)
            .WithMany(u => u.SportProfiles)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Sport)
            .WithMany()
            .HasForeignKey(x => x.SportId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.UserId, x.SportId }).IsUnique();
        builder.HasIndex(x => new { x.SportId, x.SkillLevel });
    }
}
