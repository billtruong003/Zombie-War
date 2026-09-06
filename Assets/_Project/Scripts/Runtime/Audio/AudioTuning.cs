using BillGameCore;

namespace ZombieWar.Audio
{
    /// <summary>
    /// Every audio timing/mix number the game tunes, in one place.
    ///
    /// These used to be literals scattered through the director and the service, which made the mix
    /// impossible to reason about as a whole - you could not see that the result bed was arriving
    /// before the stinger had finished without reading three files. Anything a designer would want
    /// to move belongs here.
    /// </summary>
    public static class AudioTuning
    {
        // ---- Music transitions ---------------------------------------------------------------

        /// <summary>Crossfade used when swapping one bed for another (menu <-> run). Kept short:
        /// a long crossfade is two tracks audible at once, which reads as a mistake.</summary>
        public const float MusicCrossfade = 0.35f;

        /// <summary>Fade applied to the run bed when a run ends, before the stinger lands. Short and
        /// deliberate - the stinger needs a clean floor, not a slow dissolve under it.</summary>
        public const float RunBedFadeOut = 0.28f;

        /// <summary>How far before the stinger's natural end the result bed starts rising. The bed
        /// overlaps only this much of the stinger's tail, instead of the whole 9-13s clip.</summary>
        public const float StingerTailCrossfade = 0.4f;

        /// <summary>Floor for the post-stinger wait, so a freak short variant still leaves the
        /// stinger a moment to read before the bed arrives.</summary>
        public const float MinStingerHold = 0.35f;

        /// <summary>Fade-in for the result bed itself.</summary>
        public const float ResultBedFadeIn = 0.4f;

        /// <summary>Fade applied to a result stinger that Retry/Home cuts short. Long enough not to
        /// click, short enough that the fanfare is gone before the new bed is up.</summary>
        public const float ResultStingerCancelFade = 0.25f;

        // ---- Ducking -------------------------------------------------------------------------

        /// <summary>Music multiplier while a result stinger plays. The stinger owns the moment.</summary>
        public const float ResultStingerMusicDuck = 0.0f;
        /// <summary>Combat/world SFX multiplier while a result stinger plays.</summary>
        public const float ResultStingerSfxDuck = 0.25f;
        public const float ResultDuckAttack = 0.12f;
        public const float ResultDuckRelease = 0.45f;

        /// <summary>Wave cues only dip the bed; they are frequent, so this must be subtle.</summary>
        public const float WaveCueMusicDuck = 0.72f;
        public const float WaveCueDuckAttack = 0.06f;
        public const float WaveCueDuckRelease = 0.28f;
        public const float WaveCueDuckHold = 0.18f;

        // ---- Voice pool ------------------------------------------------------------------------

        public static VoicePolicy Voices => new VoicePolicy
        {
            maxVoices = 16,
            // Three of any one cue at once. Above that a horde reads as one smeared sound anyway,
            // so extra voices cost headroom and buy nothing.
            perKeyLimit = 3,
            perKeyRetrigger = 0.045f,
        };
    }
}
