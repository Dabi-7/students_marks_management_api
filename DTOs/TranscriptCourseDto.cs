namespace MarksApi.DTOs;

public class TranscriptCourseDto
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public int CreditHours { get; set; }
    public int? Mark { get; set; }
    public string Status { get; set; } = string.Empty;
}
