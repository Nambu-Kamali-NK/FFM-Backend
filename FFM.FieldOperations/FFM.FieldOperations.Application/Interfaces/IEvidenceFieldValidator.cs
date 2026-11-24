using System.Collections.Generic;

namespace FFM.FieldOperations.Application.Interfaces
{
    public class FieldValidationError
    {
        public string Field { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }

    public class EvidenceValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public List<FieldValidationError> Errors { get; } = new();

        public void AddError(string field, string message)
        {
            Errors.Add(new FieldValidationError { Field = field, Message = message });
        }
    }

    public interface IEvidenceFieldValidator
    {
        EvidenceValidationResult Validate(
            int evidenceTypeId,
            string evidenceTypeCode,
            IReadOnlyDictionary<string, object>? readings);
    }
}

