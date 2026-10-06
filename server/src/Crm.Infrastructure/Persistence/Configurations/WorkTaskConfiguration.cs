using Crm.Domain.Tasks;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class WorkTaskConfiguration : IEntityTypeConfiguration<WorkTask>
{
    public void Configure(EntityTypeBuilder<WorkTask> task)
    {
        task.ToTable("Tasks");
        task.HasKey(t => t.Id);
        task.Property(t => t.Id).ValueGeneratedNever();
        task.Property(t => t.Title).HasMaxLength(WorkTask.TitleMaxLength).IsRequired();
        task.Property(t => t.Description).HasMaxLength(WorkTask.DescriptionMaxLength);
        task.Ignore(t => t.IsDone);
        task.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.OwnerId).OnDelete(DeleteBehavior.Restrict);
        task.HasOne<Ticket>().WithMany().HasForeignKey(t => t.TicketId).OnDelete(DeleteBehavior.Restrict);
        task.HasIndex(t => new { t.OwnerId, t.CompletedAt, t.DueAt });
        task.HasIndex(t => new { t.ReminderSentAt, t.DueAt });
    }
}
