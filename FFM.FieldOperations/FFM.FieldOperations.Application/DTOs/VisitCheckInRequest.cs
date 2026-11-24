namespace FFM.FieldOperations.Application.DTOs
{
    /// <summary>
    /// Payload for executing an on-site visit check-in.
    /// Follows the zero path parameter architectural standard.
    /// </summary>
    public class VisitCheckInRequest
    {
        public int TaskId { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public decimal? AccuracyMeters { get; set; }
        public bool? IsMockLocation { get; set; }
    }
}

