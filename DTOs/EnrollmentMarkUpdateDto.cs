using System.ComponentModel.DataAnnotations;

namespace MarksApi.DTOs;

public class EnrollmentMarkUpdateDto
{
    [Required]
    [Range(0, 100)]
    public int? Mark { get; set; }
}
