using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace BillInspector
{
    /// <summary>
    /// Lightweight binary serializer for types Unity can't handle:
    /// Dictionary, HashSet, Tuple, nested generics, etc.
    ///
    /// Design principles:
    /// - Only serialize fields marked with [BillSerialize] or fields that
    ///   Unity can't serialize (Dictionary, HashSet, etc.) on BillSerialized* base classes.
    /// - UnityEngine.Object references are stored separately in a List for Unity to manage.
    /// - Uses compact binary format via BinaryWriter/BinaryReader.
    /// </summary>
    public static class BillSerializer
    {
        private static readonly Dictionary<Type, List<FieldInfo>> s_fieldCache = new();

        /// <summary>
        /// Serialize all [BillSerialize] and non-Unity-serializable fields on the target.
        /// </summary>
        public static void SerializeObject(object target, ref byte[] data, ref List<UnityEngine.Object> objectRefs)
        {
            if (target == null) return;
            objectRefs ??= new List<UnityEngine.Object>();
            objectRefs.Clear();

            var fields = GetSerializableFields(target.GetType());
            if (fields.Count == 0)
            {
                data = null;
                return;
            }

            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms, Encoding.UTF8);

            writer.Write(fields.Count);

            foreach (var field in fields)
            {
                writer.Write(field.Name);
                var value = field.GetValue(target);
                WriteValue(writer, value, field.FieldType, objectRefs);
            }

            data = ms.ToArray();
        }

        /// <summary>
        /// Deserialize data back into the target's fields.
        /// </summary>
        public static void DeserializeObject(object target, byte[] data, List<UnityEngine.Object> objectRefs)
        {
            if (target == null || data == null || data.Length == 0) return;
            objectRefs ??= new List<UnityEngine.Object>();

            var fields = GetSerializableFields(target.GetType());
            var fieldMap = new Dictionary<string, FieldInfo>();
            foreach (var f in fields) fieldMap[f.Name] = f;

            try
            {
                using var ms = new MemoryStream(data);
                using var reader = new BinaryReader(ms, Encoding.UTF8);

                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    string name = reader.ReadString();
                    if (fieldMap.TryGetValue(name, out var field))
                    {
                        var value = ReadValue(reader, field.FieldType, objectRefs);
                        field.SetValue(target, value);
                    }
                    else
                    {
                        SkipValue(reader);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[BillSerializer] Deserialization error on {target.GetType().Name}: {e.Message}");
            }
        }

        // ═══════════════════════════════════════════════════════
        // Field discovery
        // ═══════════════════════════════════════════════════════

        /// <summary>
        /// Clears the field cache. Should be called after domain reload.
        /// Runtime code cannot use [InitializeOnLoadMethod] — editor code must call this.
        /// </summary>
        public static void ClearCache() => s_fieldCache.Clear();

        private static List<FieldInfo> GetSerializableFields(Type type)
        {
            if (s_fieldCache.TryGetValue(type, out var cached)) return cached;

            var result = new List<FieldInfo>();
            var current = type;
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            while (current != null && current != typeof(MonoBehaviour)
                   && current != typeof(ScriptableObject) && current != typeof(object))
            {
                foreach (var field in current.GetFields(flags | BindingFlags.DeclaredOnly))
                {
                    // Explicit [BillSerialize]
                    if (field.GetCustomAttribute<BillSerializeAttribute>() != null)
                    {
                        result.Add(field);
                        continue;
                    }

                    // Auto-detect: on BillSerialized* base classes, serialize types Unity can't
                    if (typeof(BillSerializedMonoBehaviour).IsAssignableFrom(type) ||
                        typeof(BillSerializedScriptableObject).IsAssignableFrom(type))
                    {
                        if (!IsUnitySerializable(field) && IsFieldSerializable(field))
                        {
                            result.Add(field);
                        }
                    }
                }
                current = current.BaseType;
            }

            s_fieldCache[type] = result;
            return result;
        }

        private static bool IsUnitySerializable(FieldInfo field)
        {
            var type = field.FieldType;

            // Unity serializes public fields (unless NonSerialized) and [SerializeField] private fields
            bool isSerializeCandidate = field.IsPublic || field.GetCustomAttribute<SerializeField>() != null;
            if (!isSerializeCandidate) return false;
            if (field.GetCustomAttribute<NonSerializedAttribute>() != null) return false;

            // Unity can serialize these types
            if (type.IsPrimitive || type == typeof(string) || type.IsEnum) return true;
            if (typeof(UnityEngine.Object).IsAssignableFrom(type)) return true;
            if (type == typeof(Vector2) || type == typeof(Vector3) || type == typeof(Vector4)) return true;
            if (type == typeof(Vector2Int) || type == typeof(Vector3Int)) return true;
            if (type == typeof(Color) || type == typeof(Color32)) return true;
            if (type == typeof(Rect) || type == typeof(RectInt)) return true;
            if (type == typeof(Bounds) || type == typeof(BoundsInt)) return true;
            if (type == typeof(Quaternion)) return true;
            if (type == typeof(AnimationCurve) || type == typeof(Gradient)) return true;
            if (type == typeof(LayerMask)) return true;

            // List<T> where T is serializable
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                return true; // simplified check

            // Arrays
            if (type.IsArray && type.GetArrayRank() == 1)
                return true; // simplified check

            // [Serializable] class/struct
            if (type.IsValueType || type.GetCustomAttribute<SerializableAttribute>() != null)
                return true;

            return false;
        }

        private static bool IsFieldSerializable(FieldInfo field)
        {
            var type = field.FieldType;
            // We can serialize: Dictionary, HashSet, Tuple, and other generics
            if (type.IsGenericType)
            {
                var gen = type.GetGenericTypeDefinition();
                if (gen == typeof(Dictionary<,>) || gen == typeof(HashSet<>) ||
                    gen == typeof(Queue<>) || gen == typeof(Stack<>) ||
                    gen == typeof(LinkedList<>))
                    return true;

                // ValueTuple
                if (type.FullName != null && type.FullName.StartsWith("System.ValueTuple"))
                    return true;
            }
            return false;
        }

        // ═══════════════════════════════════════════════════════
        // Binary write/read — compact and fast
        // ═══════════════════════════════════════════════════════

        private const byte TYPE_NULL = 0;
        private const byte TYPE_INT = 1;
        private const byte TYPE_FLOAT = 2;
        private const byte TYPE_STRING = 3;
        private const byte TYPE_BOOL = 4;
        private const byte TYPE_DICT = 5;
        private const byte TYPE_HASHSET = 6;
        private const byte TYPE_LIST = 7;
        private const byte TYPE_OBJECT_REF = 8;
        private const byte TYPE_ENUM = 9;
        private const byte TYPE_DOUBLE = 10;
        private const byte TYPE_LONG = 11;
        private const byte TYPE_TUPLE = 12;
        private const byte TYPE_VECTOR2 = 13;
        private const byte TYPE_VECTOR3 = 14;
        private const byte TYPE_COLOR = 15;

        private static void WriteValue(BinaryWriter w, object value, Type declaredType, List<UnityEngine.Object> refs)
        {
            if (value == null) { w.Write(TYPE_NULL); return; }

            var type = value.GetType();

            // UnityEngine.Object — store as reference index
            if (typeof(UnityEngine.Object).IsAssignableFrom(type))
            {
                w.Write(TYPE_OBJECT_REF);
                var obj = (UnityEngine.Object)value;
                int idx = refs.Count;
                refs.Add(obj);
                w.Write(idx);
                return;
            }

            // Primitives
            if (value is int i) { w.Write(TYPE_INT); w.Write(i); return; }
            if (value is float f) { w.Write(TYPE_FLOAT); w.Write(f); return; }
            if (value is double d) { w.Write(TYPE_DOUBLE); w.Write(d); return; }
            if (value is long l) { w.Write(TYPE_LONG); w.Write(l); return; }
            if (value is bool b) { w.Write(TYPE_BOOL); w.Write(b); return; }
            if (value is string s) { w.Write(TYPE_STRING); w.Write(s); return; }

            if (type.IsEnum) { w.Write(TYPE_ENUM); w.Write(value.ToString()); return; }

            // Unity value types
            if (value is Vector2 v2) { w.Write(TYPE_VECTOR2); w.Write(v2.x); w.Write(v2.y); return; }
            if (value is Vector3 v3) { w.Write(TYPE_VECTOR3); w.Write(v3.x); w.Write(v3.y); w.Write(v3.z); return; }
            if (value is Color c) { w.Write(TYPE_COLOR); w.Write(c.r); w.Write(c.g); w.Write(c.b); w.Write(c.a); return; }

            // Dictionary<K,V>
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
            {
                w.Write(TYPE_DICT);
                var dict = (IDictionary)value;
                var keyType = type.GetGenericArguments()[0];
                var valType = type.GetGenericArguments()[1];
                w.Write(dict.Count);
                foreach (DictionaryEntry entry in dict)
                {
                    WriteValue(w, entry.Key, keyType, refs);
                    WriteValue(w, entry.Value, valType, refs);
                }
                return;
            }

            // HashSet<T>
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(HashSet<>))
            {
                w.Write(TYPE_HASHSET);
                var elemType = type.GetGenericArguments()[0];
                int count = (int)type.GetProperty("Count").GetValue(value);
                w.Write(count);
                foreach (var item in (IEnumerable)value)
                    WriteValue(w, item, elemType, refs);
                return;
            }

            // ValueTuple — serialize each Item field
            if (type.FullName != null && type.FullName.StartsWith("System.ValueTuple"))
            {
                w.Write(TYPE_TUPLE);
                var tupleFields = type.GetFields();
                w.Write(tupleFields.Length);
                foreach (var tf in tupleFields)
                    WriteValue(w, tf.GetValue(value), tf.FieldType, refs);
                return;
            }

            // Fallback: try as list
            if (value is IList list)
            {
                w.Write(TYPE_LIST);
                var elemType = type.IsArray ? type.GetElementType()
                    : type.IsGenericType ? type.GetGenericArguments()[0] : typeof(object);
                w.Write(list.Count);
                foreach (var item in list)
                    WriteValue(w, item, elemType, refs);
                return;
            }

            // Unknown — skip
            w.Write(TYPE_NULL);
        }

        private static object ReadValue(BinaryReader r, Type declaredType, List<UnityEngine.Object> refs)
        {
            byte tag = r.ReadByte();

            switch (tag)
            {
                case TYPE_NULL: return null;
                case TYPE_INT: return r.ReadInt32();
                case TYPE_FLOAT: return r.ReadSingle();
                case TYPE_DOUBLE: return r.ReadDouble();
                case TYPE_LONG: return r.ReadInt64();
                case TYPE_BOOL: return r.ReadBoolean();
                case TYPE_STRING: return r.ReadString();

                case TYPE_ENUM:
                    var enumStr = r.ReadString();
                    if (declaredType.IsEnum)
                        return Enum.Parse(declaredType, enumStr);
                    return enumStr;

                case TYPE_VECTOR2: return new Vector2(r.ReadSingle(), r.ReadSingle());
                case TYPE_VECTOR3: return new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                case TYPE_COLOR: return new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());

                case TYPE_OBJECT_REF:
                    int idx = r.ReadInt32();
                    return (idx >= 0 && idx < refs.Count) ? refs[idx] : null;

                case TYPE_DICT:
                    return ReadDictionary(r, declaredType, refs);

                case TYPE_HASHSET:
                    return ReadHashSet(r, declaredType, refs);

                case TYPE_TUPLE:
                    return ReadTuple(r, declaredType, refs);

                case TYPE_LIST:
                    return ReadList(r, declaredType, refs);

                default: return null;
            }
        }

        private static object ReadDictionary(BinaryReader r, Type dictType, List<UnityEngine.Object> refs)
        {
            var args = dictType.IsGenericType ? dictType.GetGenericArguments() : new[] { typeof(object), typeof(object) };
            var keyType = args[0];
            var valType = args[1];
            var concreteType = typeof(Dictionary<,>).MakeGenericType(keyType, valType);
            var dict = (IDictionary)Activator.CreateInstance(concreteType);

            int count = r.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var key = ReadValue(r, keyType, refs);
                var val = ReadValue(r, valType, refs);
                if (key != null) dict[key] = val;
            }
            return dict;
        }

        private static object ReadHashSet(BinaryReader r, Type setType, List<UnityEngine.Object> refs)
        {
            var elemType = setType.IsGenericType ? setType.GetGenericArguments()[0] : typeof(object);
            var concreteType = typeof(HashSet<>).MakeGenericType(elemType);
            var set = Activator.CreateInstance(concreteType);
            var addMethod = concreteType.GetMethod("Add");

            int count = r.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                var item = ReadValue(r, elemType, refs);
                addMethod.Invoke(set, new[] { item });
            }
            return set;
        }

        private static object ReadTuple(BinaryReader r, Type tupleType, List<UnityEngine.Object> refs)
        {
            int fieldCount = r.ReadInt32();
            var tupleFields = tupleType.GetFields();
            var values = new object[fieldCount];
            for (int i = 0; i < fieldCount && i < tupleFields.Length; i++)
                values[i] = ReadValue(r, tupleFields[i].FieldType, refs);

            return Activator.CreateInstance(tupleType, values);
        }

        private static object ReadList(BinaryReader r, Type listType, List<UnityEngine.Object> refs)
        {
            var elemType = listType.IsArray ? listType.GetElementType()
                : listType.IsGenericType ? listType.GetGenericArguments()[0] : typeof(object);

            int count = r.ReadInt32();
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elemType));
            for (int i = 0; i < count; i++)
                list.Add(ReadValue(r, elemType, refs));

            if (listType.IsArray)
            {
                var arr = Array.CreateInstance(elemType, count);
                list.CopyTo(arr, 0);
                return arr;
            }
            return list;
        }

        private static void SkipValue(BinaryReader r)
        {
            // Read and discard an unknown value
            ReadValue(r, typeof(object), new List<UnityEngine.Object>());
        }
    }
}
