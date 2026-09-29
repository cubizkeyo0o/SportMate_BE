using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportMate.Domain.Entities;

namespace SportMate.Infrastructure.Persistence.Configurations;

internal sealed class PlayRequestParticipantConfiguration : IEntityTypeConfiguration<PlayRequestParticipant>
{
    public void Configure(EntityTypeBuilder<PlayRequestParticipant> builder)
    {
        builder.ToTable("play_request_participants");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.Status).IsRequired();
        builder.Property(x => x.CreatedAtUtc).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        // "Creator cannot join" and "accepted <= spots_needed" span two tables, so they are
        // enforced by the PlayRequest aggregate under a row lock rather than by constraints.
        builder.HasOne(x => x.PlayRequest)
            .WithMany(p => p.Participants)
            .HasForeignKey(x => x.PlayRequestId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.PlayRequestId, x.UserId }).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.Status });
    }
}
