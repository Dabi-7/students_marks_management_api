namespace MarksApi.DTOs;

public class CourseResponseDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int CreditHours { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
