using System.Security.Claims;
using MarksApi.Data;
using MarksApi.DTOs;
using MarksApi.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MarksApi.Controllers;

[ApiController]
[Route("api/enrollments")]
[Authorize]
public class EnrollmentsController : ControllerBase
{
    private readonly AppDbContext _context;

    public EnrollmentsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetEnrollments(CancellationToken cancellationToken)
    {
        if (User.IsInRole("Teacher"))
        {
            var enrollments = await _context.Enrollments
                .AsNoTracking()
                .OrderBy(e => e.Id)
                .Select(e => new EnrollmentResponseDto
                {
                    Id = e.Id,
                    StudentId = e.StudentId,
                    StudentRegistrationNo = e.Student.RegistrationNo,
                    StudentFullName = e.Student.FullName,
                    CourseId = e.CourseId,
                    CourseCode = e.Course.Code,
                    CourseTitle = e.Course.Title,
                    Mark = e.Mark,
                    EnrolledAtUtc = e.EnrolledAtUtc
                })
                .ToListAsync(cancellationToken);

            return Ok(enrollments);
        }

        var studentId = await GetCurrentStudentIdAsync(cancellationToken);
        if (studentId is null)
        {
            return Forbid();
        }

        var ownEnrollments = await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId.Value)
            .OrderBy(e => e.Id)
            .Select(e => new EnrollmentResponseDto
            {
                Id = e.Id,
                StudentId = e.StudentId,
                StudentRegistrationNo = e.Student.RegistrationNo,
                StudentFullName = e.Student.FullName,
                CourseId = e.CourseId,
                CourseCode = e.Course.Code,
                CourseTitle = e.Course.Title,
                Mark = e.Mark,
                EnrolledAtUtc = e.EnrolledAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(ownEnrollments);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetEnrollment(int id, CancellationToken cancellationToken)
    {
        if (User.IsInRole("Teacher"))
        {
            var enrollment = await _context.Enrollments
                .AsNoTracking()
                .Where(e => e.Id == id)
                .Select(e => new EnrollmentResponseDto
                {
                    Id = e.Id,
                    StudentId = e.StudentId,
                    StudentRegistrationNo = e.Student.RegistrationNo,
                    StudentFullName = e.Student.FullName,
                    CourseId = e.CourseId,
                    CourseCode = e.Course.Code,
                    CourseTitle = e.Course.Title,
                    Mark = e.Mark,
                    EnrolledAtUtc = e.EnrolledAtUtc
                })
                .FirstOrDefaultAsync(cancellationToken);

            return enrollment is null ? NotFound() : Ok(enrollment);
        }

        var studentId = await GetCurrentStudentIdAsync(cancellationToken);
        if (studentId is null)
        {
            return Forbid();
        }

        var enrollmentOwner = await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new { e.StudentId })
            .FirstOrDefaultAsync(cancellationToken);

        if (enrollmentOwner is null)
        {
            return NotFound();
        }

        if (enrollmentOwner.StudentId != studentId.Value)
        {
            return Forbid();
        }

        var ownEnrollment = await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new EnrollmentResponseDto
            {
                Id = e.Id,
                StudentId = e.StudentId,
                StudentRegistrationNo = e.Student.RegistrationNo,
                StudentFullName = e.Student.FullName,
                CourseId = e.CourseId,
                CourseCode = e.Course.Code,
                CourseTitle = e.Course.Title,
                Mark = e.Mark,
                EnrolledAtUtc = e.EnrolledAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        return ownEnrollment is null ? NotFound() : Ok(ownEnrollment);
    }

    [Authorize(Roles = "Teacher")]
    [HttpPost]
    public async Task<IActionResult> CreateEnrollment([FromBody] EnrollmentCreateDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var studentExists = await _context.Students.AnyAsync(s => s.Id == dto.StudentId, cancellationToken);
        if (!studentExists)
        {
            return NotFound(new { message = "Student not found." });
        }

        var courseExists = await _context.Courses.AnyAsync(c => c.Id == dto.CourseId, cancellationToken);
        if (!courseExists)
        {
            return NotFound(new { message = "Course not found." });
        }

        var duplicateExists = await _context.Enrollments.AnyAsync(e =>
            e.StudentId == dto.StudentId && e.CourseId == dto.CourseId,
            cancellationToken);

        if (duplicateExists)
        {
            return Conflict(new { message = "The student is already enrolled in this course." });
        }

        var enrollment = new Enrollment
        {
            StudentId = dto.StudentId,
            CourseId = dto.CourseId,
            Mark = null,
            EnrolledAtUtc = DateTime.UtcNow
        };

        _context.Enrollments.Add(enrollment);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateEnrollmentViolation(ex))
        {
            return Conflict(new { message = "The student is already enrolled in this course." });
        }

        var created = await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.Id == enrollment.Id)
            .Select(e => new EnrollmentResponseDto
            {
                Id = e.Id,
                StudentId = e.StudentId,
                StudentRegistrationNo = e.Student.RegistrationNo,
                StudentFullName = e.Student.FullName,
                CourseId = e.CourseId,
                CourseCode = e.Course.Code,
                CourseTitle = e.Course.Title,
                Mark = e.Mark,
                EnrolledAtUtc = e.EnrolledAtUtc
            })
            .FirstAsync(cancellationToken);

        return CreatedAtAction(nameof(GetEnrollment), new { id = enrollment.Id }, created);
    }

    [Authorize(Roles = "Teacher")]
    [HttpPut("{id:int}/mark")]
    public async Task<IActionResult> UpdateMark(int id, [FromBody] EnrollmentMarkUpdateDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var enrollment = await _context.Enrollments.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (enrollment is null)
        {
            return NotFound();
        }

        enrollment.Mark = dto.Mark;
        await _context.SaveChangesAsync(cancellationToken);

        var updated = await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new EnrollmentResponseDto
            {
                Id = e.Id,
                StudentId = e.StudentId,
                StudentRegistrationNo = e.Student.RegistrationNo,
                StudentFullName = e.Student.FullName,
                CourseId = e.CourseId,
                CourseCode = e.Course.Code,
                CourseTitle = e.Course.Title,
                Mark = e.Mark,
                EnrolledAtUtc = e.EnrolledAtUtc
            })
            .FirstAsync(cancellationToken);

        return Ok(updated);
    }

    [Authorize(Roles = "Teacher")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteEnrollment(int id, CancellationToken cancellationToken)
    {
        var enrollment = await _context.Enrollments.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (enrollment is null)
        {
            return NotFound();
        }

        _context.Enrollments.Remove(enrollment);
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
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

    private static bool IsDuplicateEnrollmentViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException postgresException
               && postgresException.SqlState == PostgresErrorCodes.UniqueViolation;
    }
}
