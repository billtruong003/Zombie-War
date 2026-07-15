using System;
using UnityEditor;
using UnityEngine;
using BillInspector;

namespace BillInspector.Editor
{
    /// <summary>
    /// Evaluates conditions from BillConditionAttribute.
    /// Now powered by ExpressionCompiler for @expressions.
    /// </summary>
    public static class ConditionEvaluator
    {
        public static bool Evaluate(BillConditionAttribute condAttr, SerializedObject serializedObject)
        {
            if (condAttr == null || serializedObject?.targetObject == null)
                return true;

            var condition = condAttr.Condition;
            var target = serializedObject.targetObject;
            var targetType = target.GetType();

            // @expression — use full compiler
            if (condition.StartsWith("@"))
                return ExpressionCompiler.EvaluateBool(condition.Substring(1), target);

            // Method reference
            var method = BillReflectionCache.GetMethod(targetType, condition);
            if (method != null && method.ReturnType == typeof(bool) && method.GetParameters().Length == 0)
                return (bool)method.Invoke(target, null);

            // Field/Property reference
            var memberValue = GetMemberValue(target, targetType, condition);
            if (memberValue == null) return true;

            // Enum comparison
            if (condAttr.CompareValue != null)
                return Equals(memberValue, condAttr.CompareValue);

            // Bool field
            if (memberValue is bool boolVal)
                return boolVal;

            return memberValue != null;
        }

        /// <summary>
        /// Evaluate a condition string (for InfoBox.VisibleIf, Button.EnableIf, etc.)
        /// </summary>
        public static bool EvaluateString(string condition, object target)
        {
            if (string.IsNullOrEmpty(condition)) return true;
            if (condition.StartsWith("@"))
                return ExpressionCompiler.EvaluateBool(condition.Substring(1), target);

            // Simple field/method
            var type = target.GetType();

            var method = BillReflectionCache.GetMethod(type, condition);
            if (method != null && method.ReturnType == typeof(bool))
                return (bool)method.Invoke(target, null);

            var val = GetMemberValue(target, type, condition);
            if (val is bool b) return b;
            return val != null;
        }

        private static object GetMemberValue(object target, Type type, string name)
        {
            return BillReflectionCache.GetMemberValue(target, type, name);
        }
    }
}
