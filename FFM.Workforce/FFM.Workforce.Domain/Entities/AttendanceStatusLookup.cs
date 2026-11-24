namespace FFM.Workforce.Domain.Entities
{
    public class AttendanceStatusLookup
    {
        public int Id { get; private set; }
        public string Code { get; private set; } = string.Empty;
        public string Description { get; private set; } = string.Empty;

        private AttendanceStatusLookup() { }

        public AttendanceStatusLookup(int id, string code, string description)
        {
            Id = id;
            Code = code;
            Description = description;
        }
    }
}
