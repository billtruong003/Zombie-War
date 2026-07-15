using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using BillInspector;

namespace BillInspector.Editor
{
    /// <summary>
    /// Validates objects using BillInspector validation attributes.
    /// Supports single object, batch, and scene-wide validation.
    /// </summary>
    public static class BillValidator
    {
        /// <summary>Validate a single object.</summary>
        public static ValidationResultList Validate(UnityEngine.Object target)
        {
            var results = new ValidationResultList();
            if (target == null) return results;

            var type = target.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // 1. Attribute-based validation on fields
            foreach (var field in type.GetFields(flags))
            {
                ValidateField(field, target, results);
            }

            // Walk base classes
            var baseType = type.BaseType;
            while (baseType != null && baseType != typeof(MonoBehaviour)
                   && baseType != typeof(ScriptableObject) && baseType != typeof(UnityEngine.Object))
            {
                foreach (var field in baseType.GetFields(flags | BindingFlags.DeclaredOnly))
                    ValidateField(field, target, results);
                baseType = baseType.BaseType;
            }

            // 2. [BillValidate] methods
            foreach (var method in type.GetMethods(flags))
            {
                if (method.GetCustomAttribute<BillValidateAttribute>() == null) continue;
                var parameters = method.GetParameters();
                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(ValidationResultList))
                {
                    method.Invoke(target, new object[] { results });
                }
            }

            // Set object name on all entries (struct — must write back by index)
            for (int i = 0; i < results.Entries.Count; i++)
            {
                if (string.IsNullOrEmpty(results.Entries[i].ObjectName))
                {
                    var entry = results.Entries[i];
                    entry.ObjectName = target.name;
                    entry.Target = target;
                    results.Entries[i] = entry;
                }
            }

            return results;
        }

        /// <summary>Validate a list of objects.</summary>
        public static ValidationResultList ValidateAll<T>(IEnumerable<T> objects) where T : UnityEngine.Object
        {
            var combined = new ValidationResultList();
            foreach (var obj in objects)
            {
                if (obj == null) continue;
                var result = Validate(obj);
                foreach (var entry in result.Entries)
                    combined.AddEntry(entry);
            }
            return combined;
        }

        /// <summary>Validate all MonoBehaviours in the active scene.</summary>
        public static ValidationResultList ValidateScene()
        {
            var all = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            var combined = new ValidationResultList();
            foreach (var mb in all)
            {
                var result = Validate(mb);
                combined.Entries.AddRange(result.Entries);
            }
            return combined;
        }

        /// <summary>Validate all ScriptableObjects in the project.</summary>
        public static ValidationResultList ValidateProjectAssets(string searchPath = "Assets")
        {
            var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { searchPath });
            var combined = new ValidationResultList();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so == null) continue;

                var result = Validate(so);
                combined.Entries.AddRange(result.Entries);
            }
            return combined;
        }

        /// <summary>Log validation results to console.</summary>
        public static void LogResults(ValidationResultList results, bool onlyErrors = false)
        {
            foreach (var entry in results.Entries)
            {
                if (onlyErrors && entry.Severity != ValidationSeverity.Error) continue;

                switch (entry.Severity)
                {
                    case ValidationSeverity.Error:
                        Debug.LogError(entry.ToString(), entry.Target);
                        break;
                    case ValidationSeverity.Warning:
                        Debug.LogWarning(entry.ToString(), entry.Target);
                        break;
                    default:
                        Debug.Log(entry.ToString(), entry.Target);
                        break;
                }
            }

            if (results.Entries.Count == 0)
                Debug.Log("[BillValidator] All validations passed!");
            else
                Debug.Log($"[BillValidator] {results.ErrorCount} errors, {results.WarningCount} warnings, " +
                          $"{results.Entries.Count - results.ErrorCount - results.WarningCount} info");
        }

        // ═══════════════════════════════════════════════════════

        private static void ValidateField(FieldInfo field, object target, ValidationResultList results)
        {
            var value = field.GetValue(target);

            // [BillRequired]
            var required = field.GetCustomAttribute<BillRequiredAttribute>();
            if (required != null)
            {
                bool empty = value == null
                    || (value is string s && string.IsNullOrEmpty(s))
                    || (value is UnityEngine.Object obj && obj == null);
                if (empty)
                    results.AddError(required.Message, field.Name);
            }

            // [BillValidateInput]
            var validate = field.GetCustomAttribute<BillValidateInputAttribute>();
            if (validate != null)
            {
                bool valid;
                if (validate.Condition.StartsWith("@"))
                {
                    valid = ExpressionCompiler.EvaluateBool(validate.Condition.Substring(1), target);
                }
                else
                {
                    var method = target.GetType().GetMethod(validate.Condition,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (method != null && method.GetParameters().Length == 1)
                        valid = (bool)method.Invoke(target, new[] { value });
                    else if (method != null && method.GetParameters().Length == 0)
                        valid = (bool)method.Invoke(target, null);
                    else
                        valid = true;
                }

                if (!valid)
                {
                    var severity = validate.MessageType == InfoType.Warning
                        ? ValidationSeverity.Warning : ValidationSeverity.Error;
                    results.Entries.Add(new ValidationEntry
                    {
                        Message = validate.Message,
                        FieldName = field.Name,
                        Severity = severity
                    });
                    if (severity == ValidationSeverity.Error) { /* ErrorCount handled by struct */ }
                }
            }

            // [BillFileExists]
            var fileExists = field.GetCustomAttribute<BillFileExistsAttribute>();
            if (fileExists != null && value is string path)
            {
                if (!string.IsNullOrEmpty(path) && !System.IO.File.Exists(path))
                    results.AddError(fileExists.Message, field.Name);
            }

            // [BillRangeValidation]
            var rangeVal = field.GetCustomAttribute<BillRangeValidationAttribute>();
            if (rangeVal != null && value is IConvertible conv)
            {
                float fval = conv.ToSingle(null);
                if (fval < rangeVal.Min || fval > rangeVal.Max)
                    results.AddError(rangeVal.Message ?? $"Value must be between {rangeVal.Min} and {rangeVal.Max}",
                        field.Name);
            }

            // [BillNotEmpty]
            var notEmpty = field.GetCustomAttribute<BillNotEmptyAttribute>();
            if (notEmpty != null)
            {
                bool isEmpty = value == null;
                if (value is ICollection col) isEmpty = col.Count == 0;
                if (value is string str) isEmpty = string.IsNullOrEmpty(str);
                if (isEmpty) results.AddError(notEmpty.Message, field.Name);
            }
        }
    }
}
