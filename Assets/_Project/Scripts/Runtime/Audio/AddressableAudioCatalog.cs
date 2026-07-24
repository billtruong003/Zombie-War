using System;
using UnityEngine;

namespace ZombieWar.Audio
{
    [CreateAssetMenu(
        fileName = "AddressableAudioCatalog",
        menuName = "ZombieWar/Audio/Addressable Audio Catalog")]
    public sealed class AddressableAudioCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Variant
        {
            public string cueKey;
            public string address;
            public string label;
            public string profile;
            public float volume = 1f;
            public float pitch = 1f;
            public float pitchVariation;
            public bool loop;
        }

        [SerializeField] private string catalogId;
        [SerializeField] private string[] preloadLabels;
        [SerializeField] private Variant[] variants;

        public string CatalogId => catalogId;
        public string[] PreloadLabels => preloadLabels;
        public Variant[] Variants => variants;

#if UNITY_EDITOR
        public void SetEditorData(string id, string[] labels, Variant[] entries)
        {
            catalogId = id;
            preloadLabels = labels;
            variants = entries;
        }
#endif
    }
}
