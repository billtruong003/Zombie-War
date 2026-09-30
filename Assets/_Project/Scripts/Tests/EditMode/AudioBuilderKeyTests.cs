using System;
using System.Collections.Generic;
using NUnit.Framework;
using ZombieWar.Editor.Audio;

namespace ZombieWar.Tests
{
    /// <summary>The audio build fills missing cue keys and never overrides a chosen one.</summary>
    public class AudioBuilderKeyTests
    {
        static readonly HashSet<string> Cues = new(StringComparer.Ordinal)
            { "sfx.weapon.shotgun.fire", "sfx.weapon.grenade.fire" };

        [Test]
        public void AChosenCueIsKept()
        {
            string key = "sfx.weapon.shotgun.fire";   // the Launcher's chosen sound
            Assert.IsFalse(ZombieWarCuratedAudioBuilder.Fill(ref key, "sfx.weapon.grenade.fire", Cues));
            Assert.AreEqual("sfx.weapon.shotgun.fire", key);
        }

        [Test]
        public void AnEmptyOrUnknownKeyIsFilled()
        {
            string empty = "";
            Assert.IsTrue(ZombieWarCuratedAudioBuilder.Fill(ref empty, "sfx.weapon.grenade.fire", Cues));
            Assert.AreEqual("sfx.weapon.grenade.fire", empty);

            string stale = "gun_fire";                // the old default, not a cue
            Assert.IsTrue(ZombieWarCuratedAudioBuilder.Fill(ref stale, "sfx.weapon.grenade.fire", Cues));
            Assert.AreEqual("sfx.weapon.grenade.fire", stale);
        }
    }
}
