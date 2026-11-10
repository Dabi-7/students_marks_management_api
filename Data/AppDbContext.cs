using MarksApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace MarksApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Student> Students => Set<Student>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Student>(entity =>
        {
            entity.HasKey(s => s.Id);

            entity.Property(s => s.RegistrationNo)
                .IsRequired()
                .HasMaxLength(20);
            entity.HasIndex(s => s.RegistrationNo)
                .IsUnique();

            entity.Property(s => s.FullName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(s => s.Email)
                .IsRequired()
                .HasMaxLength(150);
            entity.HasIndex(s => s.Email)
                .IsUnique();

            entity.Property(s => s.CreatedAtUtc)
                .IsRequired();

            entity.HasCheckConstraint("CK_Students_Email_Lowercase", "lower(email) = email");
        });

        modelBuilder.Entity<Course>(entity =>
        {
            entity.HasKey(c => c.Id);

            entity.Property(c => c.Code)
                .IsRequired()
                .HasMaxLength(10);
            entity.HasIndex(c => c.Code)
                .IsUnique();

            entity.Property(c => c.Title)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(c => c.CreditHours)
                .IsRequired();

            entity.Property(c => c.CreatedAtUtc)
                .IsRequired();

            entity.HasCheckConstraint("CK_Courses_CreditHours_Valid", "credit_hours BETWEEN 1 AND 4");
        });

        modelBuilder.Entity<Enrollment>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.StudentId)
                .IsRequired();

            entity.Property(e => e.CourseId)
                .IsRequired();

            entity.Property(e => e.Mark)
                .HasColumnType("integer");

            entity.Property(e => e.EnrolledAtUtc)
                .IsRequired();

            entity.HasIndex(e => new { e.StudentId, e.CourseId })
                .IsUnique();

            entity.HasCheckConstraint("CK_Enrollments_Mark_Valid", "mark IS NULL OR (mark >= 0 AND mark <= 100)");

            entity.HasOne(e => e.Student)
                .WithMany(s => s.Enrollments)
                .HasForeignKey(e => e.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Course)
                .WithMany(c => c.Enrollments)
                .HasForeignKey(e => e.CourseId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);

            entity.Property(u => u.Username)
                .IsRequired()
                .HasMaxLength(50);
            entity.HasIndex(u => u.Username)
                .IsUnique();

            entity.Property(u => u.PasswordHash)
                .IsRequired();

            entity.Property(u => u.Role)
                .IsRequired()
                .HasMaxLength(20);

            entity.HasOne(u => u.Student)
                .WithMany()
                .HasForeignKey(u => u.StudentId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasCheckConstraint("CK_Users_Role_Valid", "role IN ('Teacher', 'Student')");
            entity.HasCheckConstraint("CK_Users_StudentRole_Requires_Student", "role <> 'Student' OR student_id IS NOT NULL");
        });
    }
}
