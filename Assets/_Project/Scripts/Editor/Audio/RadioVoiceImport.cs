using UnityEditor;
using UnityEngine;

namespace ZombieWar.Editor.Audio
{
    /// <summary>
    /// Import settings for the agents' radio voice-over (Assets/_Project/Audio/VO/Clips, Addressables "vo/<id>").
    /// The WAVs arrive already mastered (-18 LUFS, peak -1 dBFS, mono 44.1 kHz), so import keeps
    /// them as they are: mono, Vorbis 60 % compressed in memory, loaded on demand by RadioVoice.
    /// </summary>
    public sealed class RadioVoiceImport : AssetPostprocessor
    {
        const string Folder = "Assets/_Project/Audio/VO/";

        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var importer = (AudioImporter)assetImporter;
            // The WAVs are already mono. Force To Mono must stay OFF: Unity applies its peak
            // "Normalize" only together with it, and that pushed every line to 0 dBFS peak, undoing
            // the -18 LUFS loudness matching (lines played louder/quieter than each other again).
            importer.forceToMono = false;
            importer.loadInBackground = true;
            importer.ambisonic = false;
            var s = importer.defaultSampleSettings;
            s.loadType = AudioClipLoadType.CompressedInMemory;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.6f;
            s.preloadAudioData = false;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = s;
        }
    }
}
