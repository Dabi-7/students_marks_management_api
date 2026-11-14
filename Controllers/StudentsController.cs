using System.Security.Claims;
using MarksApi.Data;
using MarksApi.DTOs;
using MarksApi.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MarksApi.Controllers;

[ApiController]
[Route("api/students")]
[Authorize]
public class StudentsController : ControllerBase
{
    private readonly AppDbContext _context;

    public StudentsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetStudents(CancellationToken cancellationToken)
    {
        if (await IsTeacherAsync(cancellationToken))
        {
            var students = await _context.Students
                .AsNoTracking()
                .OrderBy(s => s.Id)
                .Select(s => MapToResponse(s))
                .ToListAsync(cancellationToken);

            return Ok(students);
        }

        var studentId = await GetCurrentStudentIdAsync(cancellationToken);
        if (studentId is null)
        {
            return Forbid();
        }

        var student = await _context.Students
            .AsNoTracking()
            .Where(s => s.Id == studentId.Value)
            .Select(s => MapToResponse(s))
            .FirstOrDefaultAsync(cancellationToken);

        if (student is null)
        {
            return NotFound();
        }

        return Ok(new[] { student });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetStudent(int id, CancellationToken cancellationToken)
    {
        if (await IsTeacherAsync(cancellationToken))
        {
            var student = await _context.Students
                .AsNoTracking()
                .Where(s => s.Id == id)
                .Select(s => MapToResponse(s))
                .FirstOrDefaultAsync(cancellationToken);

            return student is null ? NotFound() : Ok(student);
        }

        var studentId = await GetCurrentStudentIdAsync(cancellationToken);
        if (studentId is null || studentId.Value != id)
        {
            return Forbid();
        }

        var currentStudent = await _context.Students
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => MapToResponse(s))
            .FirstOrDefaultAsync(cancellationToken);

        return currentStudent is null ? NotFound() : Ok(currentStudent);
    }

    [HttpGet("{id:int}/transcript")]
    public async Task<IActionResult> GetTranscript(int id, CancellationToken cancellationToken)
    {
        if (!await IsTeacherAsync(cancellationToken))
        {
            var studentId = await GetCurrentStudentIdAsync(cancellationToken);
            if (studentId is null || studentId.Value != id)
            {
                return Forbid();
            }
        }

        var student = await _context.Students
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new
            {
                s.Id,
                s.RegistrationNo,
                s.FullName
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (student is null)
        {
            return NotFound();
        }

        var courses = await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == id)
            .OrderBy(e => e.Course.Code)
            .Select(e => new TranscriptCourseDto
            {
                CourseId = e.CourseId,
                CourseCode = e.Course.Code,
                CourseTitle = e.Course.Title,
                CreditHours = e.Course.CreditHours,
                Mark = e.Mark,
                Status = e.Mark.HasValue
                    ? (e.Mark >= 50 ? "Pass" : "Fail")
                    : "Not Graded"
            })
            .ToListAsync(cancellationToken);

        var gradedCourses = courses.Where(c => c.Mark.HasValue).ToList();
        double? weightedAverage = null;

        if (gradedCourses.Count > 0)
        {
            var weightedSum = gradedCourses.Sum(c => c.Mark!.Value * c.CreditHours);
            var totalCredits = gradedCourses.Sum(c => c.CreditHours);

            if (totalCredits > 0)
            {
                weightedAverage = Math.Round((double)weightedSum / totalCredits, 2);
            }
        }

        var transcript = new StudentTranscriptDto
        {
            StudentId = student.Id,
            RegistrationNo = student.RegistrationNo,
            StudentName = student.FullName,
            Courses = courses,
            WeightedAverage = weightedAverage
        };

        return Ok(transcript);
    }

    [Authorize(Roles = "Teacher")]
    [HttpPost]
    public async Task<IActionResult> CreateStudent([FromBody] StudentCreateDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var registrationNo = dto.RegistrationNo.Trim();
        var email = dto.Email.Trim().ToLowerInvariant();
        var fullName = dto.FullName.Trim();

        var duplicateExists = await _context.Students.AnyAsync(s =>
            s.RegistrationNo == registrationNo || s.Email == email,
            cancellationToken);

        if (duplicateExists)
        {
            return Conflict(new { message = "A student with the same registration number or email already exists." });
        }

        var student = new Student
        {
            RegistrationNo = registrationNo,
            FullName = fullName,
            Email = email,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Students.Add(student);
        await _context.SaveChangesAsync(cancellationToken);

        var response = MapToResponse(student);
        return CreatedAtAction(nameof(GetStudent), new { id = student.Id }, response);
    }

    [Authorize(Roles = "Teacher")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateStudent(int id, [FromBody] StudentUpdateDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var student = await _context.Students.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (student is null)
        {
            return NotFound();
        }

        var email = dto.Email.Trim();
        var fullName = dto.FullName.Trim();

        var duplicateExists = await _context.Students.AnyAsync(s =>
            s.Id != id && s.Email == email,
            cancellationToken);

        if (duplicateExists)
        {
            return Conflict(new { message = "Another student already uses this email address." });
        }

        student.FullName = fullName;
        student.Email = email;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(MapToResponse(student));
    }

    [Authorize(Roles = "Teacher")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteStudent(int id, CancellationToken cancellationToken)
    {
        var student = await _context.Students
            .Include(s => s.Enrollments)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        if (student is null)
        {
            return NotFound();
        }

        var hasEnrollments = student.Enrollments.Any();
        if (hasEnrollments)
        {
            return Conflict(new { message = "Cannot delete a student who has enrolled in courses." });
        }

        var userExists = await _context.Users.AnyAsync(u => u.StudentId == id, cancellationToken);
        if (userExists)
        {
            return Conflict(new { message = "Cannot delete a student who has an associated user account." });
        }

        _context.Students.Remove(student);
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private async Task<bool> IsTeacherAsync(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return false;
        }

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        return user is not null && user.Role == "Teacher";
    }

    private async Task<int?> GetCurrentStudentIdAsync(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return null;
        }

        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        return user?.StudentId;
    }

    private static StudentResponseDto MapToResponse(Student student)
    {
        return new StudentResponseDto
        {
            Id = student.Id,
            RegistrationNo = student.RegistrationNo,
            FullName = student.FullName,
            Email = student.Email,
            CreatedAtUtc = student.CreatedAtUtc
        };
    }
}
