namespace FFM.Workforce.Domain.Entities
{
    public class AuditLog
    {
        public Guid Id { get; private set; }
        public string EntityType { get; private set; } = default!; // e.g., "Employee"
        public int EntityId { get; private set; }
        public string Action { get; private set; } = default!; // e.g., "Create"
        public int ActorUserId { get; private set; }
        public string? BeforeState { get; private set; }
        public string? AfterState { get; private set; }
        public Guid CorrelationId { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private AuditLog() { }

        public AuditLog(string entityType, int entityId, string action, int actorUserId, string? beforeState, string? afterState, Guid correlationId)
        {
            Id = Guid.NewGuid();
            EntityType = entityType;
            EntityId = entityId;
            Action = action;
            ActorUserId = actorUserId;
            BeforeState = beforeState;
            AfterState = afterState;
            CorrelationId = correlationId;
            CreatedAt = DateTime.UtcNow;
        }
    }
}
