namespace MarksApi.DTOs;

public class CourseStatisticsDto
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseTitle { get; set; } = string.Empty;
    public int TotalEnrolledStudents { get; set; }
    public int GradedStudents { get; set; }
    public double? AverageMarks { get; set; }
    public int? HighestMarks { get; set; }
    public int? LowestMarks { get; set; }
    public int PassedStudents { get; set; }
    public int FailedStudents { get; set; }
    public int UngradedStudents { get; set; }
}
