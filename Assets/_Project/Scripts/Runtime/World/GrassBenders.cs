using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.World
{
    /// <summary>
    /// Interactive grass, option A (2026-10-01): each frame the foliage shader gets up to 16 points
    /// that push plants aside and press them down: the player, whatever else submitted itself this
    /// frame (enemies, the sandbox crowd), and standing benders (a station flattening the grass on its
    /// ring). The 16 closest to the player win. No render texture and no per-plant work on the CPU.
    /// </summary>
    public sealed class GrassBenders : MonoBehaviour
    {
        const int Max = 16;
        static readonly int BendersId = Shader.PropertyToID("_ZW_Benders");
        static readonly int CountId = Shader.PropertyToID("_ZW_BenderCount");

        static readonly List<Vector4> Frame = new(64);
        static readonly List<Vector4> Standing = new();
        static readonly Vector4[] Upload = new Vector4[Max];
        static readonly float[] Dist = new float[Max];
        static GrassBenders _runner;

        public static float PlayerRadius = 1.1f;

        /// The walkers sent to the shaders last frame (position, radius), the player first: snow
        /// trails press the same feet into the snow.
        public static Vector4[] LastFrame => Upload;
        public static int LastCount { get; private set; }
        /// Set by the map streamer when the map has grass: enemies then submit themselves.
        public static bool Active;
        /// Enemies submit themselves only when the map has grass.
        public static bool EnemiesBendGrass;

        /// A bender for this frame only (call every frame while it should bend grass).
        public static void Submit(Vector3 position, float radius)
        {
            EnsureRunner();
            Frame.Add(new Vector4(position.x, position.y, position.z, radius));
        }

        /// A bender that stays until removed (returns its handle).
        public static int AddStanding(Vector3 position, float radius)
        {
            EnsureRunner();
            Standing.Add(new Vector4(position.x, position.y, position.z, radius));
            return Standing.Count - 1;
        }

        public static void RemoveStanding(Vector3 position)
        {
            for (int i = Standing.Count - 1; i >= 0; i--)
                if (((Vector3)Standing[i] - position).sqrMagnitude < 0.01f) { Standing.RemoveAt(i); return; }
        }

        static readonly int SeeThroughId = Shader.PropertyToID("_ZW_SeeThrough");
        /// Radius of the see-through cone at the player (EnvSeeThrough.hlsl); 0 turns it off.
        public static float SeeThroughRadius = 3.2f;

        public static void EnsureRunner()
        {
            if (_runner != null || !Application.isPlaying) return;
            var go = new GameObject("GrassBenders");
            DontDestroyOnLoad(go);
            _runner = go.AddComponent<GrassBenders>();
        }

        void LateUpdate()
        {
            Vector3 focus = Vector3.zero;
            var player = PlayerMovement.Instance;
            int n = 0;
            if (player != null)
            {
                focus = player.transform.position;
                Upload[0] = new Vector4(focus.x, focus.y, focus.z, PlayerRadius);
                Dist[0] = 0f;
                n = 1;
            }
            else if (Frame.Count > 0) focus = Frame[0];
            else if (Camera.main != null) focus = Camera.main.transform.position;

            void Consider(Vector4 b)
            {
                float d = ((Vector3)b - focus).sqrMagnitude;
                if (n < Max) { Upload[n] = b; Dist[n] = d; n++; return; }
                int worst = 0;
                for (int i = 1; i < Max; i++) if (Dist[i] > Dist[worst]) worst = i;
                if (d < Dist[worst] && !(player != null && worst == 0)) { Upload[worst] = b; Dist[worst] = d; }
            }
            foreach (var b in Standing) Consider(b);
            foreach (var b in Frame) Consider(b);
            Frame.Clear();

            // Decoration between the camera and the player dithers away (baked maps only).
            Shader.SetGlobalVector(SeeThroughId, player != null && Active ? new Vector4(focus.x, focus.y, focus.z, SeeThroughRadius) : Vector4.zero);

            for (int i = n; i < Max; i++) Upload[i] = Vector4.zero;
            Shader.SetGlobalVectorArray(BendersId, Upload);
            Shader.SetGlobalFloat(CountId, n);
            LastCount = n;
        }

        void OnDestroy()
        {
            if (_runner == this) _runner = null;
            Shader.SetGlobalFloat(CountId, 0f);
            Shader.SetGlobalVector(SeeThroughId, Vector4.zero);
        }
    }
}
