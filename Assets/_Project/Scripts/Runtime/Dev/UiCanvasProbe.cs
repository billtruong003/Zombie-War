using Unity.Profiling;
using UnityEngine;

namespace ZombieWar.Dev
{
    /// <summary>
    /// G12.9: how much the UI costs each frame (zw.ui.watch, then zw.ui.report), from the uGUI
    /// profiler markers, so the same numbers come from a device. Measured 04/10 in a run (editor,
    /// A/B/A in one session): ~0.22 ms/frame of UpdateBatches whether the HUD is one canvas or split
    /// into four, and whether the radio and splash canvases are on or off; batching itself
    /// ~0.006 ms. The HUD canvas split was therefore not applied.
    /// </summary>
    public static class UiCanvasProbe
    {
        // Unity 6 names: rebuild (layout + graphics) runs in WillRenderCanvases, batching in BuildBatch.
        static readonly string[] Markers = { "PostLateUpdate.PlayerUpdateCanvases", "UIEvents.WillRenderCanvases", "Canvas.BuildBatch", "UGUI.Rendering.UpdateBatches" };
        const int Capacity = 4096;
        static ProfilerRecorder[] _rec;
        static int _startFrame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Stop(); }

        static void Stop()
        {
            if (_rec != null) foreach (var r in _rec) r.Dispose();
            _rec = null;
        }

        public static void Watch()
        {
            Stop();
            _rec = new ProfilerRecorder[Markers.Length];
            var all = new System.Collections.Generic.List<Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle>();
            Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetAvailable(all);
            for (int i = 0; i < Markers.Length; i++)
            {
                // The markers sit in different categories (PlayerLoop, UI Render): find them by name.
                var handle = all.Find(h => Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetDescription(h).Name == Markers[i]);
                _rec[i] = handle.Valid ? new ProfilerRecorder(handle, Capacity, ProfilerRecorderOptions.Default) : default;
                if (handle.Valid) _rec[i].Start();
            }
            _startFrame = Time.frameCount;
            Debug.Log("[UiProbe] watching " + string.Join(", ", Markers));
        }

        public static string Report()
        {
            if (_rec == null) return "[UiProbe] not watching (zw.ui.watch)";
            int frames = Mathf.Max(1, Time.frameCount - _startFrame);
            var sb = new System.Text.StringBuilder($"[UiProbe] {frames} frames\n");
            for (int i = 0; i < _rec.Length; i++)
            {
                var r = _rec[i];
                int n = Mathf.Min(r.Count, Capacity);
                double total = 0;
                for (int k = 0; k < n; k++) total += r.GetSample(k).Value;
                // Time markers report nanoseconds; the samples cover the last n frames.
                sb.AppendLine($"  {Markers[i],-36} {(n > 0 ? total / n / 1e6 : 0):0.000} ms/frame");
            }
            Stop();
            Debug.Log(sb.ToString());
            return sb.ToString();
        }
    }
}
