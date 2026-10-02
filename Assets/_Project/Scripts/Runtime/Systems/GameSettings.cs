using BillGameCore;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace ZombieWar
{
    /// <summary>
    /// M10 Settings screen values, kept in PlayerPrefs (device settings, not profile progress, so
    /// "Delete my data" leaves them alone). <see cref="Apply"/> runs once at boot from
    /// <see cref="BootstrapEntry"/>, after Bill services exist.
    /// </summary>
    public static class GameSettings
    {
        public enum Graphics { Low, Mid, High }

        const string KMusic = "set.music", KSfx = "set.sfx", KGraphics = "set.graphics", KFps = "set.fps", KNotify = "set.notify";
        /// Shared with UIFeedback and the in-run pause panel.
        public const string KHaptics = "haptics";

        public static float Music { get => PlayerPrefs.GetFloat(KMusic, 0.8f); set { PlayerPrefs.SetFloat(KMusic, Mathf.Clamp01(value)); Bill.Audio?.SetVolume(AudioChannel.Music, Music); } }
        public static float Sfx { get => PlayerPrefs.GetFloat(KSfx, 1f); set { PlayerPrefs.SetFloat(KSfx, Mathf.Clamp01(value)); Bill.Audio?.SetVolume(AudioChannel.SFX, Sfx); } }
        public static bool Haptics { get => PlayerPrefs.GetInt(KHaptics, 1) == 1; set => PlayerPrefs.SetInt(KHaptics, value ? 1 : 0); }
        public static bool Notifications { get => PlayerPrefs.GetInt(KNotify, 1) == 1; set => PlayerPrefs.SetInt(KNotify, value ? 1 : 0); }

        public static Graphics Quality
        {
            get => (Graphics)Mathf.Clamp(PlayerPrefs.GetInt(KGraphics, (int)DeviceGuess()), 0, 2);
            set { PlayerPrefs.SetInt(KGraphics, (int)value); ApplyGraphics(); GraphicsTier.NotifyChanged(); }
        }

        /// <summary>Forgets the player's choice and goes back to the device guess.</summary>
        public static void ResetQualityToDevice()
        {
            PlayerPrefs.DeleteKey(KGraphics);
            ApplyGraphics();
            GraphicsTier.NotifyChanged();
        }

        /// <summary>30 or 60.</summary>
        public static int Fps
        {
            get => PlayerPrefs.GetInt(KFps, 60) >= 60 ? 60 : 30;
            set { PlayerPrefs.SetInt(KFps, value >= 60 ? 60 : 30); Application.targetFrameRate = Fps; }
        }

        public static void Apply()
        {
            Bill.Audio?.SetVolume(AudioChannel.Music, Music);
            Bill.Audio?.SetVolume(AudioChannel.SFX, Sfx);
            Application.targetFrameRate = Fps;
            ApplyGraphics();
        }

        /// <summary>What each Graphics option turns on. Render scale only touches the 3D world (the
        /// overlay UI stays at native resolution and sharp).</summary>
        public readonly struct Preset
        {
            public readonly float renderScale, shadowDistance; public readonly int msaa; public readonly bool hdr, postFx;
            public Preset(float scale, float shadows, int msaa, bool hdr, bool post)
            { renderScale = scale; shadowDistance = shadows; this.msaa = msaa; this.hdr = hdr; postFx = post; }
        }

        /// Low: no shadows, no post, 70% 3D. Mid: short shadows, post, 85%. High: longer shadows,
        /// 2x MSAA, HDR, full resolution.
        public static Preset PresetFor(Graphics g) => g switch
        {
            Graphics.Low => new Preset(0.7f, 0f, 1, false, false),
            Graphics.Mid => new Preset(0.85f, 25f, 1, false, true),
            _ => new Preset(1f, 40f, 2, true, true),
        };

        public static float ResolutionScale(Graphics g) => PresetFor(g).renderScale;

        /// <summary>First launch: the one device guess of the game (02/10, it was two formulas):
        /// Low under ~3 GB, at 4 cores or fewer, or with a small GPU memory; High from ~6 GB with 8
        /// cores; Mid between.</summary>
        public static Graphics DeviceDefault(int memoryMb, int cores = 8, int vramMb = 0)
        {
            if (memoryMb < 3000 || cores <= 4 || (vramMb > 0 && vramMb < 768)) return Graphics.Low;
            if (memoryMb >= 5800 && cores >= 8) return Graphics.High;
            return Graphics.Mid;
        }

        /// <summary>The guess for this device (the editor always counts as High).</summary>
        public static Graphics DeviceGuess() => Application.isEditor ? Graphics.High
            : DeviceDefault(SystemInfo.systemMemorySize, SystemInfo.processorCount, SystemInfo.graphicsMemorySize);

        static UniversalRenderPipelineAsset _runtime;
        static bool _hooked;

        static void ApplyGraphics()
        {
            if (!PlayerPrefs.HasKey(KGraphics)) PlayerPrefs.SetInt(KGraphics, (int)DeviceGuess());
            // The editor keeps the project's URP asset as authored (a runtime copy there would leak
            // into QualitySettings); players get a copy tuned to the preset. The camera's post
            // effects still follow the preset in the editor, or every play test and capture there
            // shows the game without the bloom and outline a phone renders (found 30/09).
            if (Application.isEditor)
            {
                ApplyPostFx();
                if (!_hooked) { _hooked = true; SceneManager.sceneLoaded += (_, _) => ApplyPostFx(); }
                return;
            }
            var p = PresetFor(Quality);
            if (_runtime == null)
            {
                var src = (QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline) as UniversalRenderPipelineAsset;
                if (src == null) return;
                _runtime = Object.Instantiate(src); _runtime.name = src.name + " (runtime)";
                QualitySettings.renderPipeline = _runtime;
            }
            _runtime.renderScale = p.renderScale;
            _runtime.shadowDistance = p.shadowDistance;
            _runtime.msaaSampleCount = p.msaa;
            _runtime.supportsHDR = p.hdr;
            ApplyPostFx();
            if (!_hooked) { _hooked = true; SceneManager.sceneLoaded += (_, _) => ApplyPostFx(); }
        }

        /// Post effects follow the preset on the game camera.
        static void ApplyPostFx()
        {
            var cam = Camera.main;
            if (cam != null && cam.TryGetComponent(out UniversalAdditionalCameraData data))
                data.renderPostProcessing = PresetFor(Quality).postFx;
        }
    }
}
