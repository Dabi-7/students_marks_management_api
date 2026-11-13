using System.ComponentModel.DataAnnotations;

namespace MarksApi.DTOs;

public class StudentCreateDto
{
    [Required]
    [StringLength(20, MinimumLength = 1)]
    public string RegistrationNo { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;
}
