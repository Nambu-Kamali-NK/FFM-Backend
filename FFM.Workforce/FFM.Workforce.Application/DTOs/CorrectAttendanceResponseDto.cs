namespace FFM.Workforce.Application.DTOs
{
    public class CorrectAttendanceResponseDto
    {
        public int AttendanceId { get; set; }
        public int CorrectedByUserId { get; set; }
        public string Status { get; set; } = string.Empty;
        public double NetWorkingHours { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
