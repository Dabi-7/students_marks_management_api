using System.ComponentModel.DataAnnotations;

namespace MarksApi.DTOs;

public class CourseUpdateDto
{
    [Required]
    [StringLength(10, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [StringLength(150, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [Range(1, 4)]
    public int CreditHours { get; set; }
}
