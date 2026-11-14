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
    private readonly ILogger<EnrollmentsController> _logger;

    public EnrollmentsController(AppDbContext context, ILogger<EnrollmentsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetEnrollments(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = "id",
        [FromQuery] string? sortOrder = "asc",
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<Enrollment> query = _context.Enrollments
            .AsNoTracking()
            .Include(e => e.Student)
            .Include(e => e.Course);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim();
            query = query.Where(e =>
                e.Student.RegistrationNo.Contains(normalized) ||
                e.Student.FullName.Contains(normalized) ||
                e.Course.Code.Contains(normalized) ||
                e.Course.Title.Contains(normalized));
        }

        var validSortFields = new Dictionary<string, Func<IQueryable<Enrollment>, IQueryable<Enrollment>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = q => q.OrderBy(e => e.Id),
            ["studentid"] = q => q.OrderBy(e => e.StudentId),
            ["courseid"] = q => q.OrderBy(e => e.CourseId),
            ["studentregistrationno"] = q => q.OrderBy(e => e.Student.RegistrationNo),
            ["studentfullname"] = q => q.OrderBy(e => e.Student.FullName),
            ["coursecode"] = q => q.OrderBy(e => e.Course.Code),
            ["coursetitle"] = q => q.OrderBy(e => e.Course.Title),
            ["mark"] = q => q.OrderBy(e => e.Mark ?? int.MinValue),
            ["enrolledatutc"] = q => q.OrderBy(e => e.EnrolledAtUtc)
        };

        if (!validSortFields.TryGetValue(sortBy ?? "id", out var sortFunc))
        {
            return BadRequest(new { message = "Unsupported sort field. Allowed values: id, studentId, courseId, studentRegistrationNo, studentFullName, courseCode, courseTitle, mark, enrolledAtUtc." });
        }

        query = sortFunc(query);

        if (string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase))
        {
            query = ApplyDescending(query, sortBy ?? "id");
        }

        var totalRecords = await query.CountAsync(cancellationToken);
        var totalPages = totalRecords == 0 ? 0 : (int)Math.Ceiling(totalRecords / (double)pageSize);

        var enrollments = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
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

        return Ok(new PaginatedResult<EnrollmentResponseDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalRecords = totalRecords,
            TotalPages = totalPages,
            Data = enrollments
        });
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
            _logger.LogInformation("Enrollment created for student {StudentId} in course {CourseId}.", dto.StudentId, dto.CourseId);
        }
        catch (DbUpdateException ex) when (IsDuplicateEnrollmentViolation(ex))
        {
            _logger.LogWarning(ex, "Duplicate enrollment attempted for student {StudentId} and course {CourseId}.", dto.StudentId, dto.CourseId);
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
        _logger.LogInformation("Enrollment {EnrollmentId} deleted.", id);

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

    private static IQueryable<Enrollment> ApplyDescending(IQueryable<Enrollment> query, string sortBy)
    {
        return sortBy.ToLowerInvariant() switch
        {
            "id" => query.OrderByDescending(e => e.Id),
            "studentid" => query.OrderByDescending(e => e.StudentId),
            "courseid" => query.OrderByDescending(e => e.CourseId),
            "studentregistrationno" => query.OrderByDescending(e => e.Student.RegistrationNo),
            "studentfullname" => query.OrderByDescending(e => e.Student.FullName),
            "coursecode" => query.OrderByDescending(e => e.Course.Code),
            "coursetitle" => query.OrderByDescending(e => e.Course.Title),
            "mark" => query.OrderByDescending(e => e.Mark ?? int.MinValue),
            "enrolledatutc" => query.OrderByDescending(e => e.EnrolledAtUtc),
            _ => query
        };
    }
}
