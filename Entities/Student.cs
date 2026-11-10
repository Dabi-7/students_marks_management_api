using System.ComponentModel.DataAnnotations;

namespace MarksApi.Entities;

public class Student
{
    public int Id { get; set; }

    [Required]
    [StringLength(20)]
    public string RegistrationNo { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    private string _email = string.Empty;

    [Required]
    [StringLength(150)]
    public string Email
    {
        get => _email;
        set => _email = value.Trim().ToLowerInvariant();
    }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
}