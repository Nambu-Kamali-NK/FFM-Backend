namespace FFM.Workforce.Domain.Entities
{
    /// <summary>
    /// Represents a pending domain event that will be persisted and later published to RabbitMQ.
    /// Only the fields required by the specification are included.
    /// </summary>
    public class OutboxMessage
    {
        public Guid Id { get; private set; }
        public string EventType { get; private set; } = default!; // e.g. "EmployeeCreated"
        public string Payload { get; private set; } = default!; // JSON string of the event data
        public DateTime CreatedAt { get; private set; }
        public bool Processed { get; private set; }
        public DateTime? ProcessedAt { get; private set; }
        public Guid CorrelationId { get; private set; }

        private OutboxMessage() { }

        public OutboxMessage(string eventType, string payload, Guid correlationId)
        {
            Id = Guid.NewGuid();
            EventType = eventType;
            Payload = payload;
            CorrelationId = correlationId;
            CreatedAt = DateTime.UtcNow;
            Processed = false;
        }

        public void MarkProcessed()
        {
            Processed = true;
            ProcessedAt = DateTime.UtcNow;
        }
    }
}
