using System;
using System.Collections.Generic;
using UnityEngine;

namespace BillGameCore
{
    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "BillGameCore/Audio Library")]
    public sealed class AudioLibrary : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public string key;
            public AudioClip clip;
            [Range(0f, 1f)] public float volume = 1f;
            [Range(0.1f, 3f)] public float pitch = 1f;
            public bool loop;
            [Range(0f, 0.3f)] public float pitchVariation;
        }

        public Entry[] entries;

        private Dictionary<string, Entry[]> _entriesByKey;
        private Dictionary<string, int> _lastVariantByKey;

        public Entry Get(string key)
        {
            if (_entriesByKey == null)
                RebuildLookup();
            if (!_entriesByKey.TryGetValue(key, out var variants) || variants.Length == 0)
                return null;
            if (variants.Length == 1)
                return variants[0];

            _lastVariantByKey ??= new Dictionary<string, int>(_entriesByKey.Count);
            var hasPreviousVariant = _lastVariantByKey.TryGetValue(key, out var previousVariant);
            var variantIndex = UnityEngine.Random.Range(0, variants.Length - 1);
            if (hasPreviousVariant && variantIndex >= previousVariant)
                variantIndex++;

            _lastVariantByKey[key] = variantIndex;
            return variants[variantIndex];
        }

        public void ReplaceEntries(Entry[] runtimeEntries)
        {
            entries = runtimeEntries ?? Array.Empty<Entry>();
            ClearLookup();
        }

        private void RebuildLookup()
        {
            var groupedEntries = new Dictionary<string, List<Entry>>(entries?.Length ?? 0);
            foreach (var entry in entries ?? Array.Empty<Entry>())
            {
                if (string.IsNullOrEmpty(entry.key) || entry.clip == null)
                    continue;

                if (!groupedEntries.TryGetValue(entry.key, out var variants))
                {
                    variants = new List<Entry>(4);
                    groupedEntries.Add(entry.key, variants);
                }
                variants.Add(entry);
            }

            _entriesByKey = new Dictionary<string, Entry[]>(groupedEntries.Count);
            foreach (var pair in groupedEntries)
                _entriesByKey.Add(pair.Key, pair.Value.ToArray());
            _lastVariantByKey = null;
        }

        private void OnEnable()
        {
            ClearLookup();
        }

        private void ClearLookup()
        {
            _entriesByKey = null;
            _lastVariantByKey = null;
        }
    }
}
