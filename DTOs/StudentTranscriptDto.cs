namespace MarksApi.DTOs;

public class StudentTranscriptDto
{
    public int StudentId { get; set; }
    public string RegistrationNo { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public List<TranscriptCourseDto> Courses { get; set; } = new();
    public double? WeightedAverage { get; set; }
}
