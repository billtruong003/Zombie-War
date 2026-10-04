using System.IO;
using NUnit.Framework;
using UnityEngine;
using ZombieWar.Audio;

namespace ZombieWar.Tests
{
    /// 04/10: the radio card's waveform follows the voice through envelopes measured from the WAV
    /// files (Tools/vo_envelopes.py). A new or replaced line without one would leave the bars flat.
    public class VoiceEnvelopeTests
    {
        const string Clips = "Assets/_Project/Audio/VO/Clips";

        [Test]
        public void EveryVoiceClip_HasAnEnvelope()
        {
            var missing = new System.Collections.Generic.List<string>();
            foreach (var path in Directory.GetFiles(Clips, "*.wav"))
            {
                string id = Path.GetFileNameWithoutExtension(path);
                if (!VoiceEnvelopes.Has(id)) missing.Add(id);
            }
            Assert.IsEmpty(missing, "run: python Tools/vo_envelopes.py\n" + string.Join("\n", missing));
        }

        [Test]
        public void Envelope_IsLoudMidLine_AndSilentPastTheEnd()
        {
            string id = Path.GetFileNameWithoutExtension(Directory.GetFiles(Clips, "*.wav")[0]);
            float peak = 0f;
            for (float t = 0f; t < 2f; t += 1f / 30f) peak = Mathf.Max(peak, VoiceEnvelopes.At(id, t));
            Assert.Greater(peak, 0.5f, "a spoken line reaches its loud parts");
            Assert.AreEqual(0f, VoiceEnvelopes.At(id, 600f));
            Assert.AreEqual(0f, VoiceEnvelopes.At("no_such_line", 1f));
        }
    }
}
