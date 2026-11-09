using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Api.Domain;

namespace SchoolAdmission.Api.Data;

public sealed class AdmissionDbContext(DbContextOptions<AdmissionDbContext> options) : DbContext(options)
{
    public DbSet<SchoolClass> Classes => Set<SchoolClass>();
    public DbSet<SeatHold> Holds => Set<SeatHold>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SchoolClass>(entity =>
        {
            entity.ToTable("classes", table =>
                // Database-level guard; the API validates this too and returns 400 long before this.
                table.HasCheckConstraint("ck_classes_total_seats_positive", "total_seats > 0"));
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).HasColumnName("id");
            entity.Property(c => c.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            entity.Property(c => c.TotalSeats).HasColumnName("total_seats");
            entity.Property(c => c.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<SeatHold>(entity =>
        {
            entity.ToTable("holds");
            entity.HasKey(h => h.Id);
            entity.Property(h => h.Id).HasColumnName("id");
            entity.Property(h => h.ClassId).HasColumnName("class_id");
            entity.Property(h => h.StudentId).HasColumnName("student_id").HasMaxLength(17).IsFixedLength().IsRequired();
            entity.Property(h => h.Status)
                .HasColumnName("status")
                .HasMaxLength(16)
                .HasConversion(s => s.ToApiString(), s => HoldStatusExtensions.ParseApiString(s));
            entity.Property(h => h.CreatedAt).HasColumnName("created_at");
            entity.Property(h => h.ExpiresAt).HasColumnName("expires_at");
            entity.Property(h => h.ConfirmedAt).HasColumnName("confirmed_at");
            entity.Property(h => h.CancelledAt).HasColumnName("cancelled_at");

            entity.HasOne(h => h.Class)
                .WithMany(c => c.Holds)
                .HasForeignKey(h => h.ClassId)
                .OnDelete(DeleteBehavior.Restrict);

            // Serves the seat-count queries: all active holds for a class.
            entity.HasIndex(h => new { h.ClassId, h.Status, h.ExpiresAt })
                .HasDatabaseName("ix_holds_class_status_expires");

            // Serves GET /me/holds.
            entity.HasIndex(h => h.StudentId).HasDatabaseName("ix_holds_student");

            // Rule 3 safety net: one active (HELD or CONFIRMED) application per student per class.
            // Application logic enforces this under the class row lock; the index guarantees it at
            // the database even with several backend processes writing concurrently.
            entity.HasIndex(h => new { h.ClassId, h.StudentId })
                .IsUnique()
                .HasDatabaseName("ux_holds_one_active_per_student_per_class")
                .HasFilter("status IN ('HELD', 'CONFIRMED')");
        });
    }
}
