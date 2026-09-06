namespace BillGameCore
{
    public interface IService { }
    public interface IInitializable { void Initialize(); }
    public interface IDisposableService { void Cleanup(); }
    public interface ITickable { void Tick(float dt); }
    public interface ILateTickable { void LateTick(float dt); }

    // --- Event ---
    public interface IEvent { }

    public interface IEventBus : IService
    {
        void Subscribe<T>(System.Action<T> handler) where T : IEvent;
        void SubscribeOnce<T>(System.Action<T> handler) where T : IEvent;
        void Unsubscribe<T>(System.Action<T> handler) where T : IEvent;
        void Fire<T>(T data) where T : IEvent;
        void Fire<T>() where T : struct, IEvent;
    }

    // --- Scene ---
    public interface ISceneService : IService
    {
        string CurrentSceneName { get; }
        int CurrentBuildIndex { get; }
        bool IsLoading { get; }
        System.Collections.Generic.IReadOnlyList<string> LoadedAdditiveScenes { get; }

        // Single scene loading
        void Load(string sceneName);
        void Load(string sceneName, TransitionType transition, float duration = 0.5f);
        void Load(string sceneName, TransitionType transition, float duration, EaseType ease);
        void Load(int buildIndex);

        // Additive scene management
        void LoadAdditive(string sceneName, System.Action onComplete = null);
        void Unload(string sceneName, System.Action onComplete = null);
        void UnloadAllAdditive();
        bool IsAdditiveLoaded(string sceneName);

        // Async with progress
        void LoadAsync(string sceneName, System.Action<float> onProgress = null, System.Action onComplete = null);
        void LoadWithTransition(string sceneName, TransitionType transition, float duration, EaseType ease,
            System.Action<float> onProgress = null, System.Action onComplete = null);

        // Navigation
        void Reload();
        void LoadNext();
        void LoadPrevious();
    }

    // --- Pool ---
    public interface IPoolService : IService
    {
        UnityEngine.GameObject Spawn(string key);
        UnityEngine.GameObject Spawn(string key, UnityEngine.Vector3 pos, UnityEngine.Quaternion rot);
        UnityEngine.GameObject Spawn(string key, UnityEngine.Transform parent);
        UnityEngine.GameObject Spawn(string key, UnityEngine.Vector3 pos, UnityEngine.Quaternion rot, UnityEngine.Transform parent);
        T Spawn<T>(string key) where T : UnityEngine.Component;
        T Spawn<T>(string key, UnityEngine.Vector3 pos, UnityEngine.Quaternion rot) where T : UnityEngine.Component;
        void Return(UnityEngine.GameObject obj);
        void Return(UnityEngine.GameObject obj, float delay);
        void ReturnAll(string key);
        void ReturnAll();
        void WarmUp(string key, int count);
        void Register(string key, UnityEngine.GameObject prefab, int warmCount = 5);
        int GetPooledCount(string key);
        int GetActiveCount(string key);
        string GetStats();
    }

    // --- Audio ---
    public enum AudioChannel { Master, Music, SFX, UI, Voice }

    public interface IAudioService : IService
    {
        void Play(string key);
        void Play(string key, UnityEngine.Vector3 position);
        void Play(string key, float volume);
        void Play(string key, UnityEngine.Vector3 position, float volume);
        /// <summary>Play with an extra pitch multiplier on top of the AudioLibrary entry's own pitch+variation
        /// (e.g. scale SFX pitch by throw/hit power for juice).</summary>
        void PlayPitched(string key, float pitchMultiplier, float volume = 1f);
        /// <summary>Play at an explicit priority and get back the real duration of the variant that
        /// was chosen, so a caller can time a follow-up cue against it instead of guessing.</summary>
        float PlayCue(string key, SfxPriority priority, float volume = 1f);
        float PlayCue(string key, UnityEngine.Vector3 position, SfxPriority priority, float volume = 1f);
        void PlayMusic(string key);
        void PlayMusic(string key, float fadeDuration);
        void StopMusic(float fadeDuration = 0f);
        /// <summary>Key currently on the music bus, or "" when stopped.</summary>
        string CurrentMusicKey { get; }
        /// <summary>Attenuate a channel until the returned token is released. Overlapping ducks are
        /// resolved by depth, so releasing one never restores over another that is still held.</summary>
        int Duck(AudioChannel channel, float amount, float attack);
        /// <summary>Duck a cue group. Attenuates voices ALREADY playing, not just future ones.</summary>
        int DuckGroup(CueGroup group, float amount, float attack);
        void Unduck(int token, float release);
        void ConfigureVoices(VoicePolicy policy);
        /// <summary>Play a cue that can be stopped later. Reserve for cues that genuinely need
        /// cancelling - the handle bookkeeping is not free.</summary>
        AudioCueHandle PlayManagedCue(string key, SfxPriority priority, CueGroup group, float volume = 1f);
        /// <summary>Restart a cue on the SAME dedicated voice at exact caller cadence. Built for
        /// automatic-weapon fire, where per-shot one-shots either stack voices or get dropped by the
        /// per-key policy - both of which read as fake gaps. Pass the previous handle back in.</summary>
        AudioCueHandle RetriggerCue(string key, AudioCueHandle handle, SfxPriority priority, float volume = 1f);
        /// <summary>Play a cue that must never be silently dropped, bounded to its own small
        /// same-key voice budget. For player weapon shots: every visible shot gets a transient,
        /// and once the budget is full the key's oldest (nearly finished) voice is recycled rather
        /// than the new shot being discarded by the global per-key limit.</summary>
        AudioCueHandle PlayGuaranteedTransient(string key, SfxPriority priority, int maxSameKeyVoices, float volume = 1f);
        void StopCue(AudioCueHandle handle, float fadeOut);
        bool IsCueAlive(AudioCueHandle handle);
        void SetVolume(AudioChannel channel, float volume);
        float GetVolume(AudioChannel channel);
        void Mute(AudioChannel channel);
        void Unmute(AudioChannel channel);
    }

    // --- Save ---
    public interface ISaveService : IService
    {
        void Set(string key, string value);
        void Set(string key, int value);
        void Set(string key, float value);
        void Set(string key, bool value);
        void Set<T>(string key, T value) where T : class;
        string GetString(string key, string fallback = "");
        int GetInt(string key, int fallback = 0);
        float GetFloat(string key, float fallback = 0f);
        bool GetBool(string key, bool fallback = false);
        T Get<T>(string key) where T : class;
        bool Has(string key);
        void Delete(string key);
        void SetSlot(int slot);
        void Flush();
    }

    // --- UI ---
    public interface IUIService : IService
    {
        T Open<T>() where T : BasePanel, new();
        T Open<T>(System.Action<T> setup) where T : BasePanel, new();
        void Close<T>() where T : BasePanel;
        void CloseAll();
        void Toggle<T>() where T : BasePanel, new();
        bool IsOpen<T>() where T : BasePanel;
        bool AnyOpen();
    }

    // --- Timer ---
    public interface ITimerService : IService
    {
        TimerHandle Delay(float seconds, System.Action callback);
        TimerHandle Delay(float seconds, System.Action callback, bool unscaled);
        TimerHandle Repeat(float interval, System.Action callback);
        TimerHandle Repeat(float interval, System.Action callback, int count);
        void Cancel(TimerHandle handle);
        void CancelAll();
        int ActiveCount { get; }
    }

    // --- Config ---
    public interface IConfigService : IService
    {
        string Get(string key, string fallback = "");
        int GetInt(string key, int fallback = 0);
        float GetFloat(string key, float fallback = 0f);
        bool GetBool(string key, bool fallback = false);
        void Set(string key, string value);
        bool Has(string key);
        void ApplyRemote(System.Collections.Generic.Dictionary<string, string> data);
    }

    // --- Network ---
    public enum NetworkMode { Offline, FusionHost, FusionShared, FusionClient, FusionAutoHostOrClient }
    public enum NetworkPhase { Disconnected, Connecting, InLobby, InRoom, Playing, Disconnecting }
    public enum TransitionType { None, Fade, CrossFade }

    public interface INetworkService : IService
    {
        bool IsConnected { get; }
        bool IsOffline { get; }
        NetworkMode Mode { get; }
        int PlayerCount { get; }
        bool IsHost { get; }
        void CreateRoom(string roomId, int maxPlayers = 8, System.Action onSuccess = null, System.Action<string> onFail = null);
        void JoinRoom(string roomId, System.Action onSuccess = null, System.Action<string> onFail = null);
        void LeaveRoom(System.Action onComplete = null);
        CycleHandler Cycle { get; }
    }
}
