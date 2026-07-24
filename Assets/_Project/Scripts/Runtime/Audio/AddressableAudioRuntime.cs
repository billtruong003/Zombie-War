using System;
using System.Collections;
using System.Collections.Generic;
using BillGameCore;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace ZombieWar.Audio
{
    public sealed class AddressableAudioRuntime : MonoBehaviour
    {
        private const string CatalogResourcePath = "Audio/AddressableAudioCatalog";

        private static AddressableAudioRuntime _instance;
        private readonly List<AsyncOperationHandle<IList<AudioClip>>> _retainedHandles = new();
        private readonly List<AudioLibrary.Entry> _loadedEntries = new(1024);

        private AddressableAudioCatalog _catalog;
        private AudioLibrary _library;

        public static bool IsReady { get; private set; }
        public static float Progress { get; private set; }
        public static event Action Ready;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            IsReady = false;
            Progress = 0f;
            Ready = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null)
                return;

            var root = new GameObject("[ZombieWar.AddressableAudio]");
            DontDestroyOnLoad(root);
            _instance = root.AddComponent<AddressableAudioRuntime>();
        }

        private IEnumerator Start()
        {
            _catalog = Resources.Load<AddressableAudioCatalog>(CatalogResourcePath);
            _library = BillBootstrapConfig.Instance?.defaultAudioLibrary;
            if (_catalog == null || _library == null)
            {
                Debug.LogError(
                    "[ZombieWar Audio] Addressable catalog or Bill AudioLibrary is missing. "
                    + "Run Zombie War/Audio/Build Runtime Addressables Catalog.");
                yield break;
            }

            _library.ReplaceEntries(Array.Empty<AudioLibrary.Entry>());

            var initHandle = Addressables.InitializeAsync(false);
            yield return initHandle;
            if (initHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[ZombieWar Audio] Addressables initialization failed: {initHandle.OperationException}");
                if (initHandle.IsValid())
                    Addressables.Release(initHandle);
                yield break;
            }
            Addressables.Release(initHandle);

            var labels = _catalog.PreloadLabels;
            for (var labelIndex = 0; labelIndex < labels.Length; labelIndex++)
            {
                var label = labels[labelIndex];
                var loadHandle = Addressables.LoadAssetsAsync<AudioClip>((object)label, null);
                yield return loadHandle;

                if (loadHandle.Status != AsyncOperationStatus.Succeeded)
                {
                    Debug.LogError(
                        $"[ZombieWar Audio] Failed loading label '{label}': {loadHandle.OperationException}");
                    if (loadHandle.IsValid())
                        Addressables.Release(loadHandle);
                    continue;
                }

                _retainedHandles.Add(loadHandle);
                AppendLabelEntries(label, loadHandle.Result);
                _library.ReplaceEntries(_loadedEntries.ToArray());
                Progress = (labelIndex + 1f) / labels.Length;
            }

            IsReady = _loadedEntries.Count > 0;
            if (IsReady)
            {
                Debug.Log(
                    $"[ZombieWar Audio] Runtime catalog ready: {_loadedEntries.Count} variants, "
                    + $"{_catalog.CatalogId}.");
                Ready?.Invoke();
            }
        }

        private void AppendLabelEntries(string label, IList<AudioClip> clips)
        {
            var clipsByAddress = new Dictionary<string, AudioClip>(clips.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var clip in clips)
            {
                if (clip != null)
                    clipsByAddress[clip.name] = clip;
            }

            foreach (var variant in _catalog.Variants)
            {
                if (!string.Equals(variant.label, label, StringComparison.Ordinal)
                    || !clipsByAddress.TryGetValue(variant.address, out var clip))
                    continue;

                _loadedEntries.Add(new AudioLibrary.Entry
                {
                    key = variant.cueKey,
                    clip = clip,
                    volume = variant.volume,
                    pitch = variant.pitch,
                    pitchVariation = variant.pitchVariation,
                    loop = variant.loop,
                });
            }
        }

        private void OnDestroy()
        {
            if (_library != null)
                _library.ReplaceEntries(Array.Empty<AudioLibrary.Entry>());

            foreach (var handle in _retainedHandles)
            {
                if (handle.IsValid())
                    Addressables.Release(handle);
            }
            _retainedHandles.Clear();

            if (_instance == this)
                _instance = null;
            IsReady = false;
            Progress = 0f;
        }
    }
}
