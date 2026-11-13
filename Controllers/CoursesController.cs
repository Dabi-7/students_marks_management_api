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

    public CoursesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetCourses(CancellationToken cancellationToken)
    {
        var courses = await _context.Courses
            .AsNoTracking()
            .OrderBy(c => c.Id)
            .Select(c => new CourseResponseDto
            {
                Id = c.Id,
                Code = c.Code,
                Title = c.Title,
                CreditHours = c.CreditHours,
                CreatedAtUtc = c.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return Ok(courses);
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
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
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
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
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
}
