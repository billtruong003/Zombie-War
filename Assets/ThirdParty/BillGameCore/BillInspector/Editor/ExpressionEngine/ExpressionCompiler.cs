using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BillInspector.Editor
{
    /// <summary>
    /// Compiles @expression strings into cached delegates.
    /// Supports field/property access, method calls, comparisons,
    /// boolean operators, string interpolation ($"...{field}..."),
    /// and nested member access (a.b.c).
    ///
    /// Performance: first evaluation compiles; subsequent calls use cached delegate.
    /// </summary>
    public static class ExpressionCompiler
    {
        private static readonly Dictionary<string, CompiledExpression> s_cache = new();

        /// <summary>
        /// Evaluate an expression string against a target object. Returns the result.
        /// </summary>
        public static object Evaluate(string expression, object target)
        {
            if (string.IsNullOrEmpty(expression) || target == null)
                return null;

            var key = $"{target.GetType().FullName}::{expression}";

            if (!s_cache.TryGetValue(key, out var compiled))
            {
                compiled = Compile(expression, target.GetType());
                s_cache[key] = compiled;
            }

            try
            {
                return compiled.Evaluate(target);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BillInspector] Expression error '{expression}': {e.Message}");
                return null;
            }
        }

        /// <summary>Evaluate expression and coerce to bool.</summary>
        public static bool EvaluateBool(string expression, object target)
        {
            var result = Evaluate(expression, target);
            if (result is bool b) return b;
            if (result is int i) return i != 0;
            if (result is float f) return f != 0f;
            if (result is string s) return !string.IsNullOrEmpty(s);
            return result != null;
        }

        /// <summary>Evaluate expression and coerce to string.</summary>
        public static string EvaluateString(string expression, object target)
        {
            var result = Evaluate(expression, target);
            return result?.ToString() ?? "";
        }

        /// <summary>Evaluate expression and coerce to Color.</summary>
        public static Color EvaluateColor(string expression, object target)
        {
            var result = Evaluate(expression, target);
            if (result is Color c) return c;
            return Color.white;
        }

        [UnityEditor.InitializeOnLoadMethod]
        private static void OnDomainReload() => ClearCache();

        public static void ClearCache() => s_cache.Clear();

        // ═══════════════════════════════════════════════════════
        // Compilation
        // ═══════════════════════════════════════════════════════

        private static CompiledExpression Compile(string expr, Type targetType)
        {
            expr = expr.Trim();

            // String interpolation: $"...{field}..."
            if (expr.StartsWith("$\"") && expr.EndsWith("\""))
                return new InterpolatedStringExpression(expr, targetType);

            // Boolean operators: || has lower precedence, so split on it first
            if (ContainsOutsideStrings(expr, "||"))
                return new BooleanOrExpression(expr, targetType);
            if (ContainsOutsideStrings(expr, "&&"))
                return new BooleanAndExpression(expr, targetType);

            // Ternary: condition ? a : b
            int ternaryIdx = FindTernary(expr);
            if (ternaryIdx > 0)
                return new TernaryExpression(expr, ternaryIdx, targetType);

            // Negation: !expr
            if (expr.StartsWith("!"))
                return new NegationExpression(expr.Substring(1).Trim(), targetType);

            // Comparison operators
            string[] compOps = { ">=", "<=", "!=", "==", ">", "<" };
            foreach (var op in compOps)
            {
                int idx = FindOperator(expr, op);
                if (idx > 0)
                    return new ComparisonExpression(expr, op, idx, targetType);
            }

            // Method call: MethodName()
            if (expr.EndsWith("()"))
                return new MethodCallExpression(expr.Substring(0, expr.Length - 2).Trim(), targetType);

            // Member access (possibly chained: a.b.c)
            return new MemberAccessExpression(expr, targetType);
        }

        // ═══════════════════════════════════════════════════════
        // Expression node types
        // ═══════════════════════════════════════════════════════

        private abstract class CompiledExpression
        {
            public abstract object Evaluate(object target);
        }

        private class MemberAccessExpression : CompiledExpression
        {
            private readonly MemberInfo[] _chain;
            private readonly string _literal;
            private readonly bool _isLiteral;

            public MemberAccessExpression(string expr, Type targetType)
            {
                // Try parse as literal
                if (TryParseLiteral(expr, out var lit))
                {
                    _literal = expr;
                    _isLiteral = true;
                    return;
                }

                // Build member chain
                var parts = expr.Split('.');
                var chain = new List<MemberInfo>();
                var currentType = targetType;

                foreach (var part in parts)
                {
                    var trimmed = part.Trim();
                    var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                                | BindingFlags.Static;

                    var field = currentType.GetField(trimmed, flags);
                    if (field != null)
                    {
                        chain.Add(field);
                        currentType = field.FieldType;
                        continue;
                    }

                    var prop = currentType.GetProperty(trimmed, flags);
                    if (prop != null)
                    {
                        chain.Add(prop);
                        currentType = prop.PropertyType;
                        continue;
                    }

                    // Static access: try Color.red, Vector3.zero, etc.
                    var staticType = FindStaticType(trimmed);
                    if (staticType != null)
                    {
                        currentType = staticType;
                        continue;
                    }

                    // Can't resolve — treat as literal string
                    _isLiteral = true;
                    _literal = expr;
                    return;
                }

                _chain = chain.ToArray();
            }

            public override object Evaluate(object target)
            {
                if (_isLiteral)
                    return ParseLiteral(_literal);

                object current = target;
                foreach (var member in _chain)
                {
                    if (current == null) return null;
                    current = member switch
                    {
                        FieldInfo fi => fi.GetValue(fi.IsStatic ? null : current),
                        PropertyInfo pi => pi.GetValue(pi.GetMethod.IsStatic ? null : current),
                        _ => null
                    };
                }
                return current;
            }
        }

        private class MethodCallExpression : CompiledExpression
        {
            private readonly MethodInfo _method;

            public MethodCallExpression(string methodName, Type targetType)
            {
                _method = targetType.GetMethod(methodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            }

            public override object Evaluate(object target)
            {
                return _method?.Invoke(target, null);
            }
        }

        private class ComparisonExpression : CompiledExpression
        {
            private readonly CompiledExpression _left;
            private readonly CompiledExpression _right;
            private readonly string _op;

            public ComparisonExpression(string expr, string op, int opIdx, Type targetType)
            {
                _op = op;
                var leftStr = expr.Substring(0, opIdx).Trim();
                var rightStr = expr.Substring(opIdx + op.Length).Trim();
                _left = Compile(leftStr, targetType);
                _right = Compile(rightStr, targetType);
            }

            public override object Evaluate(object target)
            {
                var l = _left.Evaluate(target);
                var r = _right.Evaluate(target);

                // Enum comparison
                if (l != null && l.GetType().IsEnum && r is string rs)
                {
                    var dotIdx = rs.LastIndexOf('.');
                    var enumName = dotIdx >= 0 ? rs.Substring(dotIdx + 1) : rs;
                    if (Enum.TryParse(l.GetType(), enumName, out var enumVal))
                        r = enumVal;
                }
                if (r != null && r.GetType().IsEnum && l is string ls)
                {
                    var dotIdx = ls.LastIndexOf('.');
                    var enumName = dotIdx >= 0 ? ls.Substring(dotIdx + 1) : ls;
                    if (Enum.TryParse(r.GetType(), enumName, out var enumVal))
                        l = enumVal;
                }

                // Numeric comparison
                if (ToDouble(l, out var ld) && ToDouble(r, out var rd))
                {
                    return _op switch
                    {
                        ">=" => ld >= rd,
                        "<=" => ld <= rd,
                        ">"  => ld > rd,
                        "<"  => ld < rd,
                        "==" => Math.Abs(ld - rd) < 0.0001,
                        "!=" => Math.Abs(ld - rd) >= 0.0001,
                        _ => false
                    };
                }

                // Equality
                return _op switch
                {
                    "==" => Equals(l, r),
                    "!=" => !Equals(l, r),
                    _ => false
                };
            }
        }

        private class BooleanAndExpression : CompiledExpression
        {
            private readonly CompiledExpression[] _parts;

            public BooleanAndExpression(string expr, Type targetType)
            {
                var parts = SplitOutsideStrings(expr, "&&");
                _parts = new CompiledExpression[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                    _parts[i] = Compile(parts[i].Trim(), targetType);
            }

            public override object Evaluate(object target)
            {
                foreach (var p in _parts)
                {
                    var r = p.Evaluate(target);
                    if (r is bool b && !b) return false;
                    if (r == null) return false;
                }
                return true;
            }
        }

        private class BooleanOrExpression : CompiledExpression
        {
            private readonly CompiledExpression[] _parts;

            public BooleanOrExpression(string expr, Type targetType)
            {
                var parts = SplitOutsideStrings(expr, "||");
                _parts = new CompiledExpression[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                    _parts[i] = Compile(parts[i].Trim(), targetType);
            }

            public override object Evaluate(object target)
            {
                foreach (var p in _parts)
                {
                    var r = p.Evaluate(target);
                    if (r is bool b && b) return true;
                }
                return false;
            }
        }

        private class NegationExpression : CompiledExpression
        {
            private readonly CompiledExpression _inner;

            public NegationExpression(string expr, Type targetType)
            {
                _inner = Compile(expr, targetType);
            }

            public override object Evaluate(object target)
            {
                var r = _inner.Evaluate(target);
                if (r is bool b) return !b;
                return r == null;
            }
        }

        private class TernaryExpression : CompiledExpression
        {
            private readonly CompiledExpression _condition;
            private readonly CompiledExpression _ifTrue;
            private readonly CompiledExpression _ifFalse;

            public TernaryExpression(string expr, int qIdx, Type targetType)
            {
                var condStr = expr.Substring(0, qIdx).Trim();
                var rest = expr.Substring(qIdx + 1);
                var colonIdx = FindColon(rest);
                if (colonIdx < 0)
                {
                    // Malformed ternary — treat entire rest as true branch, false = null
                    _condition = Compile(condStr, targetType);
                    _ifTrue = Compile(rest.Trim(), targetType);
                    _ifFalse = new MemberAccessExpression("null", targetType);
                    return;
                }
                var trueStr = rest.Substring(0, colonIdx).Trim();
                var falseStr = rest.Substring(colonIdx + 1).Trim();

                _condition = Compile(condStr, targetType);
                _ifTrue = Compile(trueStr, targetType);
                _ifFalse = Compile(falseStr, targetType);
            }

            public override object Evaluate(object target)
            {
                var cond = _condition.Evaluate(target);
                bool isTrue = cond is bool b ? b : cond != null;
                return isTrue ? _ifTrue.Evaluate(target) : _ifFalse.Evaluate(target);
            }
        }

        private class InterpolatedStringExpression : CompiledExpression
        {
            private readonly List<object> _segments; // string literal or CompiledExpression

            public InterpolatedStringExpression(string expr, Type targetType)
            {
                // Parse $"text {field:format} more text"
                _segments = new List<object>();
                var inner = expr.Substring(2, expr.Length - 3); // strip $" and "

                int i = 0;
                while (i < inner.Length)
                {
                    int braceStart = inner.IndexOf('{', i);
                    if (braceStart < 0)
                    {
                        _segments.Add(inner.Substring(i));
                        break;
                    }

                    if (braceStart > i)
                        _segments.Add(inner.Substring(i, braceStart - i));

                    int braceEnd = inner.IndexOf('}', braceStart);
                    if (braceEnd < 0) break;

                    var fieldExpr = inner.Substring(braceStart + 1, braceEnd - braceStart - 1);

                    // Handle format specifier: {field:F2}
                    string format = null;
                    var colonIdx = fieldExpr.IndexOf(':');
                    if (colonIdx >= 0)
                    {
                        format = fieldExpr.Substring(colonIdx + 1);
                        fieldExpr = fieldExpr.Substring(0, colonIdx);
                    }

                    var compiled = Compile(fieldExpr.Trim(), targetType);
                    _segments.Add(new FormatSegment { Expression = compiled, Format = format });

                    i = braceEnd + 1;
                }
            }

            public override object Evaluate(object target)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var seg in _segments)
                {
                    if (seg is string s)
                    {
                        sb.Append(s);
                    }
                    else if (seg is FormatSegment fs)
                    {
                        var val = fs.Expression.Evaluate(target);
                        if (fs.Format != null && val is IFormattable fmt)
                            sb.Append(fmt.ToString(fs.Format, null));
                        else
                            sb.Append(val?.ToString() ?? "null");
                    }
                }
                return sb.ToString();
            }

            private class FormatSegment
            {
                public CompiledExpression Expression;
                public string Format;
            }
        }

        // ═══════════════════════════════════════════════════════
        // Parser utilities
        // ═══════════════════════════════════════════════════════

        private static bool TryParseLiteral(string expr, out object value)
        {
            value = null;
            if (expr == "true") { value = true; return true; }
            if (expr == "false") { value = false; return true; }
            if (expr == "null") { value = null; return true; }
            if (int.TryParse(expr, out int i)) { value = i; return true; }
            if (float.TryParse(expr, out float f)) { value = f; return true; }
            if (expr.EndsWith("f") && float.TryParse(expr.TrimEnd('f'), out float f2)) { value = f2; return true; }
            if (expr.StartsWith("\"") && expr.EndsWith("\""))
            {
                value = expr.Substring(1, expr.Length - 2);
                return true;
            }
            return false;
        }

        private static object ParseLiteral(string expr)
        {
            TryParseLiteral(expr, out var val);
            return val ?? expr;
        }

        private static bool ToDouble(object val, out double result)
        {
            result = 0;
            if (val is int i) { result = i; return true; }
            if (val is float f) { result = f; return true; }
            if (val is double d) { result = d; return true; }
            if (val is long l) { result = l; return true; }
            if (val is IConvertible c) { try { result = c.ToDouble(null); return true; } catch { } }
            return false;
        }

        private static bool ContainsOutsideStrings(string expr, string op)
        {
            int depth = 0;
            bool inString = false;
            for (int i = 0; i < expr.Length - op.Length + 1; i++)
            {
                if (expr[i] == '\\' && inString && i + 1 < expr.Length) { i++; continue; }
                if (expr[i] == '"') inString = !inString;
                if (inString) continue;
                if (expr[i] == '(' || expr[i] == '{') depth++;
                if (expr[i] == ')' || expr[i] == '}') depth--;
                if (depth == 0 && expr.Substring(i, op.Length) == op) return true;
            }
            return false;
        }

        private static string[] SplitOutsideStrings(string expr, string op)
        {
            var result = new List<string>();
            int depth = 0; bool inStr = false; int last = 0;
            for (int i = 0; i < expr.Length - op.Length + 1; i++)
            {
                if (expr[i] == '\\' && inStr && i + 1 < expr.Length) { i++; continue; }
                if (expr[i] == '"') inStr = !inStr;
                if (inStr) continue;
                if (expr[i] == '(' || expr[i] == '{') depth++;
                if (expr[i] == ')' || expr[i] == '}') depth--;
                if (depth == 0 && expr.Substring(i, op.Length) == op)
                {
                    result.Add(expr.Substring(last, i - last));
                    last = i + op.Length;
                }
            }
            result.Add(expr.Substring(last));
            return result.ToArray();
        }

        private static int FindOperator(string expr, string op)
        {
            int depth = 0; bool inStr = false;
            for (int i = 0; i < expr.Length - op.Length + 1; i++)
            {
                if (expr[i] == '\\' && inStr && i + 1 < expr.Length) { i++; continue; }
                if (expr[i] == '"') inStr = !inStr;
                if (inStr) continue;
                if (expr[i] == '(') depth++;
                if (expr[i] == ')') depth--;
                if (depth == 0 && expr.Substring(i, op.Length) == op) return i;
            }
            return -1;
        }

        private static int FindTernary(string expr)
        {
            int depth = 0; bool inStr = false;
            for (int i = 0; i < expr.Length; i++)
            {
                if (expr[i] == '\\' && inStr && i + 1 < expr.Length) { i++; continue; }
                if (expr[i] == '"') inStr = !inStr;
                if (inStr) continue;
                if (expr[i] == '(') depth++;
                if (expr[i] == ')') depth--;
                if (depth == 0 && expr[i] == '?') return i;
            }
            return -1;
        }

        private static int FindColon(string expr)
        {
            int depth = 0; bool inStr = false;
            for (int i = 0; i < expr.Length; i++)
            {
                char c = expr[i];
                if (c == '\\' && inStr && i + 1 < expr.Length) { i++; continue; }
                if (c == '"') inStr = !inStr;
                if (inStr) continue;
                if (c == '(' || c == '?') depth++;
                if (c == ')') depth--;
                if (depth == 0 && c == ':') return i;
            }
            Debug.LogWarning($"[BillInspector] Malformed ternary expression — no ':' found in: {expr}");
            return -1;
        }

        private static Type FindStaticType(string name)
        {
            // Common Unity/System types for static access
            return name switch
            {
                "Color" => typeof(Color),
                "Vector2" => typeof(Vector2),
                "Vector3" => typeof(Vector3),
                "Vector4" => typeof(Vector4),
                "Mathf" => typeof(Mathf),
                "Time" => typeof(Time),
                "Application" => typeof(Application),
                "Debug" => typeof(Debug),
                "Random" => typeof(UnityEngine.Random),
                _ => null
            };
        }
    }
}
