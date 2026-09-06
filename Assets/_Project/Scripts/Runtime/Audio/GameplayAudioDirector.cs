using System.Collections;
using BillGameCore;
using UnityEngine;

namespace ZombieWar.Audio
{
    /// <summary>Runtime-only audio glue for the shippable loop. It listens to existing global
    /// gameplay events, so scenes and UI need no audio references or authoring changes.</summary>
    public sealed class GameplayAudioDirector : MonoBehaviour
    {
        /// <summary>No dedicated seamless result loop exists in the curated set, so the hub loop
        /// stands in for the result screen (MVP fallback, see AUDIO_SHIP_LIST).</summary>
        private const string ResultMusicKey = "music.hub";

        private static GameplayAudioDirector _instance;

        private bool _subscribed;
        private string _desiredMusic = "";
        private Coroutine _resultRoutine;
        private bool _resultStingerPlayed;

        // Duck tokens held for the duration of the result sequence. Held as fields rather than
        // locals so a cancelled sequence can release exactly the ducks it took.
        private int _musicDuckToken;
        private int _combatDuckToken;
        private int _worldDuckToken;

        // Handle for the result stinger so Retry/Home can actually silence it. Without this the
        // 9-13 second clip kept playing straight over the new run or hub bed.
        private AudioCueHandle _resultStinger;

        // Bumped by every music decision. The result sequence checks it after each wait, so a Retry
        // or Home landing mid-stinger makes the pending bed a no-op instead of a late surprise.
        private int _transition;

        private Vector3 _lastFootstepPosition;
        private bool _hasFootstepPosition;
        private float _footstepDistance;
        private float _nextFootstepTime;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var root = new GameObject("[ZombieWar.GameplayAudio]");
            DontDestroyOnLoad(root);
            _instance = root.AddComponent<GameplayAudioDirector>();
        }

        private IEnumerator Start()
        {
            while (!Bill.IsReady || Bill.Events == null) yield return null;
            Bill.Audio?.ConfigureVoices(AudioTuning.Voices);
            Subscribe();
            AddressableAudioRuntime.Ready += OnAudioReady;

            string state = Bill.State?.CurrentName ?? "";
            if (state == "Menu") SetMusic("music.hub", false);
            else if (state == "Gameplay") SetMusic("music.run.stage1", false);
        }

        private void Subscribe()
        {
            if (_subscribed) return;
            Bill.Events.Subscribe<StateChangedEvent>(OnStateChanged);
            Bill.Events.Subscribe<WaveStartedEvent>(OnWaveStarted);
            Bill.Events.Subscribe<WaveClearedEvent>(OnWaveCleared);
            Bill.Events.Subscribe<RunFinishedEvent>(OnRunFinished);
            Bill.Events.Subscribe<PlayerDamagedEvent>(OnPlayerDamaged);
            Bill.Events.Subscribe<PlayerDiedEvent>(OnPlayerDied);
            Bill.Events.Subscribe<PickupCollectedEvent>(OnPickupCollected);
            _subscribed = true;
        }

        private void OnDestroy()
        {
            AddressableAudioRuntime.Ready -= OnAudioReady;
            if (_subscribed && Bill.Events != null)
            {
                Bill.Events.Unsubscribe<StateChangedEvent>(OnStateChanged);
                Bill.Events.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
                Bill.Events.Unsubscribe<WaveClearedEvent>(OnWaveCleared);
                Bill.Events.Unsubscribe<RunFinishedEvent>(OnRunFinished);
                Bill.Events.Unsubscribe<PlayerDamagedEvent>(OnPlayerDamaged);
                Bill.Events.Unsubscribe<PlayerDiedEvent>(OnPlayerDied);
                Bill.Events.Unsubscribe<PickupCollectedEvent>(OnPickupCollected);
            }
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (!AddressableAudioRuntime.IsReady || Bill.State?.CurrentName != "Gameplay")
            {
                _hasFootstepPosition = false;
                return;
            }

            var player = PlayerMovement.Instance;
            if (player == null)
            {
                _hasFootstepPosition = false;
                return;
            }

            Vector3 position = player.transform.position;
            if (!_hasFootstepPosition)
            {
                _lastFootstepPosition = position;
                _hasFootstepPosition = true;
                return;
            }

            Vector3 delta = position - _lastFootstepPosition;
            delta.y = 0f;
            _lastFootstepPosition = position;
            _footstepDistance += delta.magnitude;
            if (_footstepDistance < 1.55f || Time.time < _nextFootstepTime) return;

            _footstepDistance = 0f;
            _nextFootstepTime = Time.time + 0.22f;
            // Low: the first thing that should lose its voice when the horde is loud.
            Bill.Audio?.PlayCue("sfx.footstep.player.earth", position, SfxPriority.Low, 0.36f);
        }

        private void OnAudioReady()
        {
            if (string.IsNullOrEmpty(_desiredMusic)) return;
            Bill.Audio?.PlayMusic(_desiredMusic, AudioTuning.MusicCrossfade);
        }

        // State names come from GameStateMachine.Name (type name minus "State"): Menu, Loading,
        // Gameplay, GameOver. Loading is deliberately unhandled - it is a sub-second passthrough and
        // cutting the bed there would leave a hole on every Retry. GameOver is owned by
        // OnRunFinished, which runs the stinger sequence and then hands over to the result bed.
        private void OnStateChanged(StateChangedEvent e)
        {
            if (e.To == "Menu")
            {
                _resultStingerPlayed = false;
                SetMusic(ResultMusicKey, false);
            }
            else if (e.To == "Gameplay")
            {
                _resultStingerPlayed = false;
                SetMusic("music.run.stage1", true);
            }
        }

        /// <summary>
        /// Single entry point for "this is the bed that should be playing now". Cancels any result
        /// sequence still in flight first, which is what makes Retry and Home instant: whatever the
        /// stinger had queued stops mattering the moment a new bed is requested.
        ///
        /// The service itself ignores a same-key request, so re-entering a state never restarts the
        /// track - this method does not need to second-guess it.
        /// </summary>
        private void SetMusic(string key, bool playRunCue)
        {
            CancelResultSequence();
            _transition++;

            _desiredMusic = key;
            if (!AddressableAudioRuntime.IsReady) return;

            bool alreadyPlaying = Bill.Audio?.CurrentMusicKey == key;
            if (playRunCue && !alreadyPlaying)
                Bill.Audio?.PlayCue("stinger.run.start", SfxPriority.High, 0.82f);

            Bill.Audio?.PlayMusic(key, AudioTuning.MusicCrossfade);
        }

        /// <summary>Releases the result ducks and drops the queued bed. Safe to call repeatedly.</summary>
        private void CancelResultSequence()
        {
            if (_resultRoutine != null)
            {
                StopCoroutine(_resultRoutine);
                _resultRoutine = null;
            }

            // Fade the stinger out rather than hard-cutting it: Retry/Home should not leave a
            // victory fanfare playing over the next bed, but a hard stop on a 10s tail is a click.
            if (_resultStinger.IsAssigned)
            {
                Bill.Audio?.StopCue(_resultStinger, AudioTuning.ResultStingerCancelFade);
                _resultStinger = AudioCueHandle.None;
            }
            ReleaseResultDucks();
        }

        private void ReleaseResultDucks()
        {
            if (_musicDuckToken != 0)
            {
                Bill.Audio?.Unduck(_musicDuckToken, AudioTuning.ResultDuckRelease);
                _musicDuckToken = 0;
            }
            if (_combatDuckToken != 0)
            {
                Bill.Audio?.Unduck(_combatDuckToken, AudioTuning.ResultDuckRelease);
                _combatDuckToken = 0;
            }
            if (_worldDuckToken != 0)
            {
                Bill.Audio?.Unduck(_worldDuckToken, AudioTuning.ResultDuckRelease);
                _worldDuckToken = 0;
            }
        }

        // Exactly one stinger per run. RunDirector guards its own terminal path, but this component
        // survives scene reloads, so it does not get to assume that.
        private void OnRunFinished(RunFinishedEvent e)
        {
            if (_resultStingerPlayed) return;
            _resultStingerPlayed = true;

            _desiredMusic = "";
            CancelResultSequence();
            _resultRoutine = StartCoroutine(ResultSequence(e.Summary.Outcome == RunOutcome.Victory));
        }

        /// <summary>
        /// run bed fades out -> stinger owns the mix -> bed rises under the stinger's tail.
        ///
        /// The bed is timed off the duration the service reports for the variant it actually chose.
        /// Victory/defeat variants run 9-13 seconds, so the previous fixed 2.6s wait dropped the bed
        /// squarely on top of the stinger. Waiting for its real length minus one short tail
        /// crossfade is the difference between a transition and a collision.
        /// </summary>
        private IEnumerator ResultSequence(bool victory)
        {
            int generation = ++_transition;

            // Clear the floor and fire the stinger in the same frame. The bed is ducked to zero on a
            // fast attack and fading out underneath, so there is no audible overlap - and crucially
            // the stinger is NOT behind a wait. A run ending usually drives a state change within a
            // frame or two; anything waited on before the stinger can be cancelled by it, which is
            // exactly how the victory cue ended up never playing.
            Bill.Audio?.StopMusic(AudioTuning.RunBedFadeOut);
            _musicDuckToken = Bill.Audio?.Duck(AudioChannel.Music, AudioTuning.ResultStingerMusicDuck, AudioTuning.ResultDuckAttack) ?? 0;
            // Duck the combat and world buses, never the Critical bus - that is what keeps the
            // stinger itself clear while everything under it steps back.
            _combatDuckToken = Bill.Audio?.DuckGroup(CueGroup.Combat, AudioTuning.ResultStingerSfxDuck, AudioTuning.ResultDuckAttack) ?? 0;
            _worldDuckToken = Bill.Audio?.DuckGroup(CueGroup.World, AudioTuning.ResultStingerSfxDuck, AudioTuning.ResultDuckAttack) ?? 0;

            _resultStinger = Bill.Audio?.PlayManagedCue(
                victory ? "stinger.victory" : "stinger.defeat",
                SfxPriority.Critical, CueGroup.Critical, 0.9f) ?? AudioCueHandle.None;
            float stingerLength = _resultStinger.Duration;

            // Hold until the stinger is nearly done, then let the bed rise through its tail only.
            float hold = Mathf.Max(AudioTuning.MinStingerHold, stingerLength - AudioTuning.StingerTailCrossfade);
            // Unscaled: the result overlay pauses the game, and a scaled wait would never elapse.
            yield return new WaitForSecondsRealtime(hold);
            if (generation != _transition) yield break;

            ReleaseResultDucks();
            _resultStinger = AudioCueHandle.None;   // ran to completion; nothing left to cancel
            _desiredMusic = ResultMusicKey;
            Bill.Audio?.PlayMusic(ResultMusicKey, AudioTuning.ResultBedFadeIn);
            _resultRoutine = null;
        }

        private void OnWaveStarted(WaveStartedEvent e) => StartCoroutine(WaveCue("stinger.wave.start", 0.78f));

        private void OnWaveCleared(WaveClearedEvent e) => StartCoroutine(WaveCue("stinger.wave.clear", 0.82f));

        /// <summary>Wave cues dip the bed briefly so they cut through without a full result-style duck.</summary>
        private IEnumerator WaveCue(string key, float volume)
        {
            if (!AddressableAudioRuntime.IsReady) yield break;

            int token = Bill.Audio?.Duck(AudioChannel.Music, AudioTuning.WaveCueMusicDuck, AudioTuning.WaveCueDuckAttack) ?? 0;
            float length = Bill.Audio?.PlayCue(key, SfxPriority.High, volume) ?? 0f;

            yield return new WaitForSecondsRealtime(Mathf.Max(AudioTuning.WaveCueDuckHold, length));
            if (token != 0) Bill.Audio?.Unduck(token, AudioTuning.WaveCueDuckRelease);
        }

        private static void OnPlayerDamaged(PlayerDamagedEvent e) =>
            Bill.Audio?.PlayCue("sfx.player.hurt", SfxPriority.Medium, 0.62f);

        private static void OnPlayerDied(PlayerDiedEvent e) =>
            Bill.Audio?.PlayCue("sfx.player.death", SfxPriority.Critical, 0.78f);

        private static void OnPickupCollected(PickupCollectedEvent e)
        {
            string key = e.Effect switch
            {
                PickupEffect.Health => "sfx.pickup.health",
                PickupEffect.Bomb => "sfx.pickup.bomb",
                _ => e.Kind == PlayerProfile.CurrencyKind.Gem ? "sfx.pickup.gem" : "sfx.pickup.coin",
            };
            Bill.Audio?.PlayCue(key, SfxPriority.Medium, 0.55f);
        }
    }
}
