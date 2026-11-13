using System.ComponentModel.DataAnnotations;

namespace MarksApi.DTOs;

public class StudentUpdateDto
{
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;
}
