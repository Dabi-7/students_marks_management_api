using System.ComponentModel.DataAnnotations;

namespace MarksApi.Entities;

public class User
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = "Student";

    public int? StudentId { get; set; }

    public Student? Student { get; set; }
}