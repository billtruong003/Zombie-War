using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.Dev
{
    /// UI Labs only: loops a shader property (dissolve progress) so the effect can be judged in the
    /// editor and in Play. Real screens drive the same property with BillTween.
    [ExecuteAlways]
    public sealed class UILabsDemo : MonoBehaviour
    {
        [SerializeField] private Graphic target;
        [SerializeField] private string property = "_Progress";
        [SerializeField] private float period = 3f;

        Material _material;
        int _id;

        void Update()
        {
            if (_material == null)
            {
                if (target == null || target.material == null) return;
                _material = target.material;
                _id = Shader.PropertyToID(property);
            }
            // 0 → 1 in the first 40 %, hold shown, 1 → 0 in the next 40 %, hold hidden.
            float t = Mathf.Repeat(Application.isPlaying ? Time.time : (float)UnityEditor_Time(), period) / period;
            float v = t < 0.4f ? t / 0.4f : t < 0.5f ? 1f : t < 0.9f ? 1f - (t - 0.5f) / 0.4f : 0f;
            _material.SetFloat(_id, Mathf.SmoothStep(0f, 1f, v));
        }

        static double UnityEditor_Time()
        {
#if UNITY_EDITOR
            return UnityEditor.EditorApplication.timeSinceStartup;
#else
            return Time.time;
#endif
        }
    }
}
