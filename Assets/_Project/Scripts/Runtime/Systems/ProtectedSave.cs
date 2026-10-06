using System;
using System.Security.Cryptography;
using System.Text;
using BillGameCore;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// Keeps the profile unreadable in PlayerPrefs, so it cannot be edited with a text editor on a
    /// rooted phone (gems, guns). The value is AES-encrypted with a key bound to this device and app;
    /// an edited value no longer decrypts into a profile, which the profile loader already treats as
    /// damaged (it falls back to the last good backup). Values written before this existed are read as
    /// plain JSON and come back encrypted on the next save. Only the keys in <see cref="IsProtected"/>
    /// change; everything else passes straight through to the wrapped service.
    /// This stops casual editing, not a determined attacker with the app's code (the key is derived
    /// on the device); server-side checks are the answer there.
    /// </summary>
    public sealed class ProtectedSave : ISaveService
    {
        public const string Prefix = "hc1:";
        readonly ISaveService _inner;
        readonly byte[] _key;
        readonly Func<string, bool> _isProtected;

        public ProtectedSave(ISaveService inner, Func<string, bool> isProtected, string deviceSecret = null)
        {
            _inner = inner;
            _isProtected = isProtected;
            string secret = deviceSecret ?? (SystemInfo.deviceUniqueIdentifier + "|" + Application.identifier);
            using var sha = SHA256.Create();
            _key = sha.ComputeHash(Encoding.UTF8.GetBytes("hordecall-save-v1|" + secret));
        }

        public ISaveService Inner => _inner;

        public void Set<T>(string key, T value) where T : class
        {
            if (!_isProtected(key)) { _inner.Set(key, value); return; }
            _inner.Set(key, value == null ? "" : Protect(JsonUtility.ToJson(value)));
        }

        public T Get<T>(string key) where T : class
        {
            if (!_isProtected(key)) return _inner.Get<T>(key);
            string raw = _inner.GetString(key);
            if (string.IsNullOrEmpty(raw)) return null;
            if (!raw.StartsWith(Prefix, StringComparison.Ordinal)) return _inner.Get<T>(key);   // written before encryption
            string json = Unprotect(raw);
            if (json == null) return null;
            try { return JsonUtility.FromJson<T>(json); } catch (Exception) { return null; }
        }

        /// <summary>Encrypts JSON into "hc1:" + base64(IV + ciphertext).</summary>
        public string Protect(string json)
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.GenerateIV();
            using var enc = aes.CreateEncryptor();
            byte[] plain = Encoding.UTF8.GetBytes(json);
            byte[] cipher = enc.TransformFinalBlock(plain, 0, plain.Length);
            var all = new byte[aes.IV.Length + cipher.Length];
            Buffer.BlockCopy(aes.IV, 0, all, 0, aes.IV.Length);
            Buffer.BlockCopy(cipher, 0, all, aes.IV.Length, cipher.Length);
            return Prefix + Convert.ToBase64String(all);
        }

        /// <summary>The JSON inside a protected value, or null when it was edited or is not ours.</summary>
        public string Unprotect(string raw)
        {
            try
            {
                byte[] all = Convert.FromBase64String(raw.Substring(Prefix.Length));
                if (all.Length < 32) return null;
                using var aes = Aes.Create();
                aes.Key = _key;
                var iv = new byte[16];
                Buffer.BlockCopy(all, 0, iv, 0, 16);
                aes.IV = iv;
                using var dec = aes.CreateDecryptor();
                return Encoding.UTF8.GetString(dec.TransformFinalBlock(all, 16, all.Length - 16));
            }
            catch (Exception) { return null; }
        }

        // ── everything else passes through ──
        public void Set(string key, string value) => _inner.Set(key, value);
        public void Set(string key, int value) => _inner.Set(key, value);
        public void Set(string key, float value) => _inner.Set(key, value);
        public void Set(string key, bool value) => _inner.Set(key, value);
        public string GetString(string key, string fallback = "") => _inner.GetString(key, fallback);
        public int GetInt(string key, int fallback = 0) => _inner.GetInt(key, fallback);
        public float GetFloat(string key, float fallback = 0f) => _inner.GetFloat(key, fallback);
        public bool GetBool(string key, bool fallback = false) => _inner.GetBool(key, fallback);
        public bool Has(string key) => _inner.Has(key);
        public void Delete(string key) => _inner.Delete(key);
        public void SetSlot(int slot) => _inner.SetSlot(slot);
        public void Flush() => _inner.Flush();
    }
}
