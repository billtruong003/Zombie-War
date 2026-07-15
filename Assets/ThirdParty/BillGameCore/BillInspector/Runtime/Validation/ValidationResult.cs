using System.Collections.Generic;

namespace BillInspector
{
    public enum ValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    public struct ValidationEntry
    {
        public string Message;
        public string FieldName;
        public string ObjectName;
        public ValidationSeverity Severity;
        public UnityEngine.Object Target;

        public override string ToString()
        {
            var prefix = Severity switch
            {
                ValidationSeverity.Error => "[ERROR]",
                ValidationSeverity.Warning => "[WARN]",
                _ => "[INFO]"
            };
            var obj = string.IsNullOrEmpty(ObjectName) ? "" : $" on '{ObjectName}'";
            var field = string.IsNullOrEmpty(FieldName) ? "" : $".{FieldName}";
            return $"{prefix}{obj}{field}: {Message}";
        }
    }

    /// <summary>
    /// Collects validation results. Passed to [BillValidate] methods.
    /// </summary>
    public class ValidationResultList
    {
        public List<ValidationEntry> Entries { get; } = new();

        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }
        public bool HasErrors => ErrorCount > 0;
        public bool IsValid => ErrorCount == 0;

        public void AddError(string message, string fieldName = null)
        {
            Entries.Add(new ValidationEntry
            {
                Message = message,
                FieldName = fieldName,
                Severity = ValidationSeverity.Error
            });
            ErrorCount++;
        }

        public void AddWarning(string message, string fieldName = null)
        {
            Entries.Add(new ValidationEntry
            {
                Message = message,
                FieldName = fieldName,
                Severity = ValidationSeverity.Warning
            });
            WarningCount++;
        }

        public void AddInfo(string message, string fieldName = null)
        {
            Entries.Add(new ValidationEntry
            {
                Message = message,
                FieldName = fieldName,
                Severity = ValidationSeverity.Info
            });
        }

        /// <summary>Add a pre-built entry and update severity counts.</summary>
        public void AddEntry(ValidationEntry entry)
        {
            Entries.Add(entry);
            switch (entry.Severity)
            {
                case ValidationSeverity.Error: ErrorCount++; break;
                case ValidationSeverity.Warning: WarningCount++; break;
            }
        }

        public void Clear()
        {
            Entries.Clear();
            ErrorCount = 0;
            WarningCount = 0;
        }
    }
}
