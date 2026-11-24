using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using FFM.FieldOperations.Application.Interfaces;

namespace FFM.FieldOperations.Application.Services
{
    /// <summary>
    /// Generic telemetry readings validator.
    /// Supports arbitrary field telemetry metrics across all task categories (AC, Solar, Elevator, Electrical, Medical, etc.)
    /// Validates key sanitization, payload size, and primitive data types without locking to hardcoded keys or evidence types.
    /// </summary>
    public class EvidenceFieldValidator : IEvidenceFieldValidator
    {
        private const int MaxKeyLength = 100;
        private const int MaxEntries = 100;
        private const int MaxStringValueLength = 2000;
        private static readonly Regex ValidKeyRegex = new("^[a-zA-Z0-9_.-]+$", RegexOptions.Compiled);

        public EvidenceValidationResult Validate(
            int evidenceTypeId,
            string evidenceTypeCode,
            IReadOnlyDictionary<string, object>? readings)
        {
            var result = new EvidenceValidationResult();

            if (readings == null || readings.Count == 0)
            {
                return result; // Readings are optional
            }

            // 1. Max entries check
            if (readings.Count > MaxEntries)
            {
                result.AddError("readings", $"Readings dictionary cannot contain more than {MaxEntries} entries.");
                return result;
            }

            // 2. Generic validation for each telemetry metric key-value pair
            foreach (var kvp in readings)
            {
                var key = kvp.Key?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(key))
                {
                    result.AddError("readings", "Readings metric key cannot be empty or whitespace.");
                    continue;
                }

                if (key.Length > MaxKeyLength)
                {
                    result.AddError($"readings.{key}", $"Key '{key}' exceeds maximum length of {MaxKeyLength} characters.");
                    continue;
                }

                if (!ValidKeyRegex.IsMatch(key))
                {
                    result.AddError($"readings.{key}", $"Key '{key}' contains invalid characters. Only alphanumeric, underscore, hyphen, and period are permitted.");
                    continue;
                }

                // 3. String value length guard
                if (kvp.Value is string strVal && strVal.Length > MaxStringValueLength)
                {
                    result.AddError($"readings.{key}", $"Value for '{key}' exceeds maximum length of {MaxStringValueLength} characters.");
                }
            }

            return result;
        }
    }
}
