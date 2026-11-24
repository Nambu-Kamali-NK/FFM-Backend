namespace FFM.Workforce.Application.DTOs
{
    public class AttendanceCheckOutRequest
    {
        public string? CheckOutNotes { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
    }
}
