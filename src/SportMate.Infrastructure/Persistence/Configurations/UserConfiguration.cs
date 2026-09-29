using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportMate.Domain.Entities;

namespace SportMate.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        // Case-insensitive collation makes the unique index reject "A@x.com" when "a@x.com" exists.
        builder.Property(x => x.Email)
            .HasMaxLength(User.EmailMaxLength)
            .UseCollation("utf8mb4_0900_ai_ci")
            .IsRequired();
        builder.HasIndex(x => x.Email).IsUnique();

        builder.Property(x => x.PasswordHash).HasMaxLength(User.PasswordHashMaxLength).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(User.DisplayNameMaxLength).IsRequired();
        builder.Property(x => x.PhoneNumber).HasMaxLength(User.PhoneNumberMaxLength);
        builder.Property(x => x.AvatarUrl).HasMaxLength(User.AvatarUrlMaxLength);
        builder.Property(x => x.Role).IsRequired();
        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();
    }
}
