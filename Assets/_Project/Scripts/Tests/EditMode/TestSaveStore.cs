using System.Collections.Generic;
using BillGameCore;
using UnityEngine;

namespace ZombieWar.Tests
{
    /// In-memory ISaveService for profile tests (same "s0_" key prefix as SaveService). Shared by the
    /// newer tests; older fixtures still carry their own copy.
    internal sealed class TestSaveStore : ISaveService
    {
        public readonly Dictionary<string, string> store = new();
        public int flushCount, setCount;
        public bool throwOnSet;
        private string K(string key) => "s0_" + key;

        public void Set(string key, string val) { setCount++; store[K(key)] = val; }
        public void Set(string key, int val) => store[K(key)] = val.ToString();
        public void Set(string key, float val) => store[K(key)] = val.ToString();
        public void Set(string key, bool val) => store[K(key)] = val ? "1" : "0";
        public void Set<T>(string key, T val) where T : class
        {
            if (throwOnSet) throw new System.IO.IOException("disk full");
            setCount++;
            store[K(key)] = JsonUtility.ToJson(val);
        }
        public string GetString(string key, string fb = "") => store.TryGetValue(K(key), out var v) ? v : fb;
        public int GetInt(string key, int fb = 0) => fb;
        public float GetFloat(string key, float fb = 0f) => fb;
        public bool GetBool(string key, bool fb = false) => fb;
        public T Get<T>(string key) where T : class
        {
            if (!store.TryGetValue(K(key), out var j) || string.IsNullOrEmpty(j)) return null;
            try { return JsonUtility.FromJson<T>(j); } catch { return null; }
        }
        public bool Has(string key) => store.ContainsKey(K(key));
        public void Delete(string key) => store.Remove(K(key));
        public void SetSlot(int slot) { }
        public void Flush() => flushCount++;
    }
}
