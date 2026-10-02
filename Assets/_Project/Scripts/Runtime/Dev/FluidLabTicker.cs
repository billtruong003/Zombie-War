using UnityEngine;

namespace ZombieWar.Dev
{
    /// <summary>
    /// Fluid lab (2026-10-02): keeps the water, toxic, ice and lava shaders moving in Edit mode, so
    /// their materials can be tuned in the editor and seen animate. Out of Play, the editor only
    /// renders on demand and shader time stands still; this asks for a player loop update and a
    /// scene view repaint about 30 times a second, and only while its scene is loaded (it lives in
    /// Scenes/Dev/FluidLab.unity). Does nothing in Play or in a build.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class FluidLabTicker : MonoBehaviour
    {
        [Tooltip("Repaints per second in Edit mode. Higher looks smoother and costs the editor more.")]
        [SerializeField, Range(10, 60)] private int rate = 30;

#if UNITY_EDITOR
        double _last;

        void OnEnable()
        {
            // The lab shows the fluids without a map: no baked map light.
            Shader.SetGlobalFloat("_ZWMapLightOn", 0f);
            Shader.SetGlobalFloat("_ZWPlanarShadowOn", 0f);
            UnityEditor.EditorApplication.update += Tick;
        }

        void OnDisable() => UnityEditor.EditorApplication.update -= Tick;

        void Tick()
        {
            if (Application.isPlaying) return;
            double now = UnityEditor.EditorApplication.timeSinceStartup;
            if (now - _last < 1.0 / rate) return;
            _last = now;
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            UnityEditor.SceneView.RepaintAll();
        }
#endif
    }
}
