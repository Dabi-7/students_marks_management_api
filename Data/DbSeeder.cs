using MarksApi.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace MarksApi.Data;

public class DbSeeder
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public DbSeeder(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task SeedAsync()
    {
        await _context.Database.MigrateAsync();

        var seedPassword = _configuration["SeedSettings:DefaultPassword"]
            ?? throw new InvalidOperationException("Seed password is not configured. Set 'SeedSettings:DefaultPassword'.");

        var studentSeedData = new[]
        {
            new { RegistrationNo = "S001", FullName = "Alice Johnson", Email = "alice@example.com" },
            new { RegistrationNo = "S002", FullName = "Bob Smith", Email = "bob@example.com" }
        };

        foreach (var studentSeed in studentSeedData)
        {
            var exists = await _context.Students.AnyAsync(s => s.RegistrationNo == studentSeed.RegistrationNo || s.Email == studentSeed.Email);
            if (!exists)
            {
                _context.Students.Add(new Student
                {
                    RegistrationNo = studentSeed.RegistrationNo,
                    FullName = studentSeed.FullName,
                    Email = studentSeed.Email,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();

        var courseSeedData = new[]
        {
            new { Code = "CS101", Title = "Introduction to Computer Science", CreditHours = 3 },
            new { Code = "MATH201", Title = "Discrete Mathematics", CreditHours = 3 },
            new { Code = "ENG102", Title = "Academic Writing", CreditHours = 2 }
        };

        foreach (var courseSeed in courseSeedData)
        {
            var exists = await _context.Courses.AnyAsync(c => c.Code == courseSeed.Code);
            if (!exists)
            {
                _context.Courses.Add(new Course
                {
                    Code = courseSeed.Code,
                    Title = courseSeed.Title,
                    CreditHours = courseSeed.CreditHours,
                    CreatedAtUtc = DateTime.UtcNow
                });
            }
        }

        await _context.SaveChangesAsync();

        var studentMap = await _context.Students
            .Where(s => studentSeedData.Select(x => x.RegistrationNo).Contains(s.RegistrationNo))
            .ToDictionaryAsync(s => s.RegistrationNo, s => s);

        var courseMap = await _context.Courses
            .Where(c => courseSeedData.Select(x => x.Code).Contains(c.Code))
            .ToDictionaryAsync(c => c.Code, c => c);

        foreach (var studentSeed in studentSeedData)
        {
            var student = studentMap[studentSeed.RegistrationNo];

            foreach (var courseSeed in courseSeedData)
            {
                var course = courseMap[courseSeed.Code];
                var enrollmentExists = await _context.Enrollments.AnyAsync(e =>
                    e.StudentId == student.Id && e.CourseId == course.Id);

                if (!enrollmentExists)
                {
                    _context.Enrollments.Add(new Enrollment
                    {
                        StudentId = student.Id,
                        CourseId = course.Id,
                        Mark = null,
                        EnrolledAtUtc = DateTime.UtcNow
                    });
                }
            }
        }

        await _context.SaveChangesAsync();

        var userSeedData = new[]
        {
            new { Username = "teacher", Role = "Teacher", StudentRegistrationNo = (string?)null },
            new { Username = "student1", Role = "Student", StudentRegistrationNo = "S001" },
            new { Username = "student2", Role = "Student", StudentRegistrationNo = "S002" }
        };

        var hasher = new PasswordHasher<User>();

        foreach (var userSeed in userSeedData)
        {
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == userSeed.Username);
            if (existingUser is not null)
            {
                continue;
            }

            int? studentId = userSeed.StudentRegistrationNo is null
                ? null
                : studentMap[userSeed.StudentRegistrationNo].Id;

            var user = new User
            {
                Username = userSeed.Username,
                Role = userSeed.Role,
                StudentId = studentId,
                PasswordHash = string.Empty
            };

            user.PasswordHash = hasher.HashPassword(user, seedPassword);
            _context.Users.Add(user);
        }

        await _context.SaveChangesAsync();
    }
}
