namespace FFM.Workforce.Application.DTOs
{
    public class AttendanceCheckInRequest
    {
        public string? CheckInNotes { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}
