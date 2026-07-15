using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BillInspector.Editor
{
    /// <summary>
    /// Resolves values from various sources: field name, method name,
    /// @expression, $interpolation.
    ///
    /// Used by: BillLabelText, BillDropdown, BillGUIColor, BillTitle, etc.
    /// </summary>
    public static class ValueResolver
    {
        /// <summary>
        /// Resolve a string that may be a field ref, method ref, @expression, or $interpolation.
        /// </summary>
        public static string ResolveString(string source, object target)
        {
            if (string.IsNullOrEmpty(source) || target == null)
                return source;

            // @expression
            if (source.StartsWith("@"))
                return ExpressionCompiler.EvaluateString(source.Substring(1), target);

            // $fieldName — dynamic title from field value
            if (source.StartsWith("$"))
            {
                var fieldName = source.Substring(1);
                var val = GetMemberValue(target, fieldName);
                return val?.ToString() ?? source;
            }

            return source;
        }

        /// <summary>
        /// Resolve a Color from @expression or fixed values.
        /// </summary>
        public static Color ResolveColor(string source, object target, Color fallback)
        {
            if (string.IsNullOrEmpty(source)) return fallback;
            if (source.StartsWith("@"))
                return ExpressionCompiler.EvaluateColor(source.Substring(1), target);
            return fallback;
        }

        /// <summary>
        /// Resolve an IEnumerable from a method, field, or property name.
        /// </summary>
        public static IEnumerable<T> ResolveEnumerable<T>(string source, object target)
        {
            if (string.IsNullOrEmpty(source) || target == null)
                yield break;

            var type = target.GetType();

            // Try method
            var method = BillReflectionCache.GetMethod(type, source);
            if (method != null)
            {
                var result = method.Invoke(target, null);
                if (result is IEnumerable<T> typedEnum)
                {
                    foreach (var item in typedEnum) yield return item;
                    yield break;
                }
                if (result is IEnumerable untypedEnum)
                {
                    foreach (var item in untypedEnum)
                    {
                        if (item is T typed) yield return typed;
                    }
                    yield break;
                }
            }

            // Try field
            var val = GetMemberValue(target, source);
            if (val is IEnumerable<T> fieldEnum)
            {
                foreach (var item in fieldEnum) yield return item;
            }
            else if (val is IEnumerable fieldUntypedEnum)
            {
                foreach (var item in fieldUntypedEnum)
                {
                    if (item is T typed) yield return typed;
                }
            }
        }

        /// <summary>
        /// Resolve a value from member name (field or property).
        /// </summary>
        public static object ResolveValue(string source, object target)
        {
            if (string.IsNullOrEmpty(source) || target == null)
                return null;

            if (source.StartsWith("@"))
                return ExpressionCompiler.Evaluate(source.Substring(1), target);

            return GetMemberValue(target, source);
        }

        private static object GetMemberValue(object target, string name)
        {
            return BillReflectionCache.GetMemberValue(target, target.GetType(), name);
        }
    }
}
