using System.Collections.Generic;
using BillGameCore;
using NUnit.Framework;
using UnityEngine;

namespace ZombieWar.Tests
{
    public class ProtectedSaveTests
    {
        private class InMemorySave : ISaveService
        {
            public readonly Dictionary<string, string> store = new();
            public void Set(string key, string val) => store[key] = val;
            public void Set(string key, int val) => store[key] = val.ToString();
            public void Set(string key, float val) => store[key] = val.ToString();
            public void Set(string key, bool val) => store[key] = val ? "1" : "0";
            public void Set<T>(string key, T val) where T : class => store[key] = JsonUtility.ToJson(val);
            public string GetString(string key, string fb = "") => store.TryGetValue(key, out var v) ? v : fb;
            public int GetInt(string key, int fb = 0) => store.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : fb;
            public float GetFloat(string key, float fb = 0f) => store.TryGetValue(key, out var v) && float.TryParse(v, out var f) ? f : fb;
            public bool GetBool(string key, bool fb = false) => store.TryGetValue(key, out var v) ? v == "1" : fb;
            public T Get<T>(string key) where T : class
            {
                if (!store.TryGetValue(key, out var j) || string.IsNullOrEmpty(j)) return null;
                try { return JsonUtility.FromJson<T>(j); } catch { return null; }
            }
            public bool Has(string key) => store.ContainsKey(key);
            public void Delete(string key) => store.Remove(key);
            public void SetSlot(int slot) { }
            public void Flush() { }
        }

        [System.Serializable] class Wallet { public long gem; public string gun; }

        InMemorySave _raw;
        ProtectedSave _save;

        [SetUp]
        public void SetUp()
        {
            _raw = new InMemorySave();
            _save = new ProtectedSave(_raw, k => k == "profile", "test-device");
        }

        [Test]
        public void WritesAreUnreadable_AndReadBack()
        {
            _save.Set("profile", new Wallet { gem = 120, gun = "vector" });
            StringAssert.StartsWith(ProtectedSave.Prefix, _raw.store["profile"]);
            StringAssert.DoesNotContain("vector", _raw.store["profile"]);
            var back = _save.Get<Wallet>("profile");
            Assert.AreEqual(120, back.gem);
            Assert.AreEqual("vector", back.gun);
        }

        [Test]
        public void APlainOldSave_IsRead_ThenWrittenEncrypted()
        {
            _raw.Set("profile", new Wallet { gem = 7 });
            Assert.AreEqual(7, _save.Get<Wallet>("profile").gem);
            _save.Set("profile", _save.Get<Wallet>("profile"));
            StringAssert.StartsWith(ProtectedSave.Prefix, _raw.store["profile"]);
        }

        [Test]
        public void AnEditedValue_ReadsAsDamaged()
        {
            _save.Set("profile", new Wallet { gem = 5 });
            string v = _raw.store["profile"];
            char[] c = v.ToCharArray();
            c[c.Length - 6] = c[c.Length - 6] == 'A' ? 'B' : 'A';
            _raw.store["profile"] = new string(c);
            Assert.IsNull(_save.Get<Wallet>("profile"));
        }

        [Test]
        public void AnotherDevice_CannotRead()
        {
            _save.Set("profile", new Wallet { gem = 9 });
            var other = new ProtectedSave(_raw, k => k == "profile", "other-device");
            Assert.IsNull(other.Get<Wallet>("profile"));
        }

        [Test]
        public void CopyingTheRawString_KeepsItReadable()
        {
            _save.Set("profile", new Wallet { gem = 3 });
            _save.Set("backup", _save.GetString("profile"));     // what the profile's backup step does
            var protectBoth = new ProtectedSave(_raw, k => k == "profile" || k == "backup", "test-device");
            Assert.AreEqual(3, protectBoth.Get<Wallet>("backup").gem);
        }

        [Test]
        public void OtherKeys_PassThrough()
        {
            _save.Set("settings", new Wallet { gem = 1 });
            Assert.AreEqual(JsonUtility.ToJson(new Wallet { gem = 1 }), _raw.store["settings"]);
        }
    }
}
