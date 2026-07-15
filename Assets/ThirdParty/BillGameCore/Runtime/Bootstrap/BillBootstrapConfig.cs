using System;
using System.Collections.Generic;
using UnityEngine;

namespace BillGameCore
{
    [CreateAssetMenu(fileName = "BillBootstrapConfig", menuName = "BillGameCore/Bootstrap Config", order = -100)]
    public class BillBootstrapConfig : ScriptableObject
    {
        [Header("Scene")]
        public bool enforceBootstrapScene = true;
        public string defaultGameScene = "";
        public bool returnToEditSceneInEditor = true;

        [Header("Dev Tools")]
        public bool includeDebugOverlay = true;
        public bool includeCheatConsole = true;
        public bool showOverlayOnStartup = false;
        public bool enableTracing = true;

        [Header("Network")]
        public NetworkMode defaultNetworkMode = NetworkMode.Offline;

        [Header("Pool")]
        public PoolDefinition[] defaultPools;

        [Header("Audio")]
        public AudioLibrary defaultAudioLibrary;
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float musicVolume = 0.8f;
        [Range(0f, 1f)] public float sfxVolume = 1f;

        [Header("Performance")]
        public int targetFrameRate = 60;
        [Range(0, 2)] public int vSyncCount = 0;

        private static BillBootstrapConfig _inst;
        public static BillBootstrapConfig Instance
        {
            get
            {
                if (_inst == null) _inst = Resources.Load<BillBootstrapConfig>("BillBootstrapConfig");
                return _inst;
            }
        }
    }

    // -------------------------------------------------------

    [Serializable]
    public class PoolDefinition
    {
        public string key;
        public GameObject prefab;
        [Min(0)] public int warmCount = 10;
        [Min(0)] public int maxSize = 0;
        [Min(0f)] public float autoReturnTime = 0f;
    }

    // -------------------------------------------------------

    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "BillGameCore/Audio Library")]
    public class AudioLibrary : ScriptableObject
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
        private Dictionary<string, Entry> _map;

        public Entry Get(string key)
        {
            if (_map == null) Rebuild();
            return _map.TryGetValue(key, out var e) ? e : null;
        }

        private void Rebuild()
        {
            _map = new Dictionary<string, Entry>(entries?.Length ?? 0);
            if (entries == null) return;
            foreach (var e in entries)
                if (!string.IsNullOrEmpty(e.key) && e.clip != null)
                    _map[e.key] = e;
        }

        void OnEnable() => _map = null;
    }

    // -------------------------------------------------------

    [CreateAssetMenu(fileName = "GameConfig", menuName = "BillGameCore/Game Config")]
    public class GameConfigAsset : ScriptableObject
    {
        [Serializable]
        public class Entry { public string key; public string value; }
        public Entry[] entries;
    }
}
