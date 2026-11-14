using MarksApi.Data;
using MarksApi.DTOs;
using MarksApi.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace MarksApi.Controllers;

[ApiController]
[Route("api/courses")]
[Authorize]
public class CoursesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<CoursesController> _logger;

    public CoursesController(AppDbContext context, ILogger<CoursesController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetCourses(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? sortBy = "id",
        [FromQuery] string? sortOrder = "asc",
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<Course> query = _context.Courses.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim();
            query = query.Where(c =>
                c.Code.Contains(normalized) ||
                c.Title.Contains(normalized));
        }

        var validSortFields = new Dictionary<string, Func<IQueryable<Course>, IQueryable<Course>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = q => q.OrderBy(c => c.Id),
            ["code"] = q => q.OrderBy(c => c.Code),
            ["title"] = q => q.OrderBy(c => c.Title),
            ["credithours"] = q => q.OrderBy(c => c.CreditHours),
            ["createdatutc"] = q => q.OrderBy(c => c.CreatedAtUtc)
        };

        if (!validSortFields.TryGetValue(sortBy ?? "id", out var sortFunc))
        {
            return BadRequest(new { message = "Unsupported sort field. Allowed values: id, code, title, creditHours, createdAtUtc." });
        }

        query = sortFunc(query);

        if (string.Equals(sortOrder, "desc", StringComparison.OrdinalIgnoreCase))
        {
            query = ApplyDescending(query, sortBy ?? "id");
        }

        var totalRecords = await query.CountAsync(cancellationToken);
        var totalPages = totalRecords == 0 ? 0 : (int)Math.Ceiling(totalRecords / (double)pageSize);

        var courses = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CourseResponseDto
            {
                Id = c.Id,
                Code = c.Code,
                Title = c.Title,
                CreditHours = c.CreditHours,
                CreatedAtUtc = c.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(new PaginatedResult<CourseResponseDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalRecords = totalRecords,
            TotalPages = totalPages,
            Data = courses
        });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetCourse(int id, CancellationToken cancellationToken)
    {
        var course = await _context.Courses
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CourseResponseDto
            {
                Id = c.Id,
                Code = c.Code,
                Title = c.Title,
                CreditHours = c.CreditHours,
                CreatedAtUtc = c.CreatedAtUtc
            })
            .FirstOrDefaultAsync(cancellationToken);

        return course is null ? NotFound() : Ok(course);
    }

    [Authorize(Roles = "Teacher")]
    [HttpGet("{id:int}/statistics")]
    public async Task<IActionResult> GetCourseStatistics(int id, CancellationToken cancellationToken)
    {
        var course = await _context.Courses
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new { c.Id, c.Code, c.Title })
            .FirstOrDefaultAsync(cancellationToken);

        if (course is null)
        {
            return NotFound();
        }

        var aggregate = await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.CourseId == id)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Graded = g.Count(e => e.Mark.HasValue),
                Average = g.Where(e => e.Mark.HasValue).Average(e => (double?)e.Mark),
                Highest = g.Where(e => e.Mark.HasValue).Max(e => (int?)e.Mark),
                Lowest = g.Where(e => e.Mark.HasValue).Min(e => (int?)e.Mark),
                Passed = g.Count(e => e.Mark.HasValue && e.Mark >= 50),
                Failed = g.Count(e => e.Mark.HasValue && e.Mark < 50)
            })
            .FirstOrDefaultAsync(cancellationToken);

        var total = aggregate?.Total ?? 0;
        var graded = aggregate?.Graded ?? 0;

        var response = new CourseStatisticsDto
        {
            CourseId = course.Id,
            CourseCode = course.Code,
            CourseTitle = course.Title,
            TotalEnrolledStudents = total,
            GradedStudents = graded,
            AverageMarks = aggregate?.Average,
            HighestMarks = aggregate?.Highest,
            LowestMarks = aggregate?.Lowest,
            PassedStudents = aggregate?.Passed ?? 0,
            FailedStudents = aggregate?.Failed ?? 0,
            UngradedStudents = total - graded
        };

        return Ok(response);
    }

    [Authorize(Roles = "Teacher")]
    [HttpPost]
    public async Task<IActionResult> CreateCourse([FromBody] CourseCreateDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var code = dto.Code.Trim().ToUpperInvariant();
        var title = dto.Title.Trim();

        if (string.IsNullOrWhiteSpace(code))
        {
            ModelState.AddModelError(nameof(dto.Code), "Code cannot be empty or whitespace.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            ModelState.AddModelError(nameof(dto.Title), "Title cannot be empty or whitespace.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var duplicateExists = await _context.Courses.AnyAsync(c => c.Code == code, cancellationToken);
        if (duplicateExists)
        {
            return Conflict(new { message = "A course with the same code already exists." });
        }

        var course = new Course
        {
            Code = code,
            Title = title,
            CreditHours = dto.CreditHours,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.Courses.Add(course);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Course {CourseCode} created successfully.", course.Code);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogError(ex, "Failed to create course with code {CourseCode} due to a uniqueness conflict.", code);
            return Conflict(new { message = "A course with the same code already exists." });
        }

        var response = MapToResponse(course);
        return CreatedAtAction(nameof(GetCourse), new { id = course.Id }, response);
    }

    [Authorize(Roles = "Teacher")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateCourse(int id, [FromBody] CourseUpdateDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var course = await _context.Courses.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (course is null)
        {
            return NotFound();
        }

        var code = dto.Code.Trim().ToUpperInvariant();
        var title = dto.Title.Trim();

        if (string.IsNullOrWhiteSpace(code))
        {
            ModelState.AddModelError(nameof(dto.Code), "Code cannot be empty or whitespace.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            ModelState.AddModelError(nameof(dto.Title), "Title cannot be empty or whitespace.");
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var duplicateExists = await _context.Courses.AnyAsync(c => c.Id != id && c.Code == code, cancellationToken);
        if (duplicateExists)
        {
            return Conflict(new { message = "Another course already uses this code." });
        }

        course.Code = code;
        course.Title = title;
        course.CreditHours = dto.CreditHours;

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Course {CourseId} updated successfully.", course.Id);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            _logger.LogError(ex, "Failed to update course {CourseId} due to a uniqueness conflict.", id);
            return Conflict(new { message = "Another course already uses this code." });
        }

        return Ok(MapToResponse(course));
    }

    [Authorize(Roles = "Teacher")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteCourse(int id, CancellationToken cancellationToken)
    {
        var course = await _context.Courses
            .Include(c => c.Enrollments)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (course is null)
        {
            return NotFound();
        }

        if (course.Enrollments.Any())
        {
            return Conflict(new { message = "Cannot delete a course that has enrolled students." });
        }

        _context.Courses.Remove(course);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Course {CourseId} deleted successfully.", id);

        return NoContent();
    }

    private static CourseResponseDto MapToResponse(Course course)
    {
        return new CourseResponseDto
        {
            Id = course.Id,
            Code = course.Code,
            Title = course.Title,
            CreditHours = course.CreditHours,
            CreatedAtUtc = course.CreatedAtUtc
        };
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException postgresException
               && postgresException.SqlState == PostgresErrorCodes.UniqueViolation;
    }

    private static IQueryable<Course> ApplyDescending(IQueryable<Course> query, string sortBy)
    {
        return sortBy.ToLowerInvariant() switch
        {
            "id" => query.OrderByDescending(c => c.Id),
            "code" => query.OrderByDescending(c => c.Code),
            "title" => query.OrderByDescending(c => c.Title),
            "credithours" => query.OrderByDescending(c => c.CreditHours),
            "createdatutc" => query.OrderByDescending(c => c.CreatedAtUtc),
            _ => query
        };
    }
}
