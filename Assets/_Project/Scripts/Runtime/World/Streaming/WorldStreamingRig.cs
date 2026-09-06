using UnityEngine;

namespace ZombieWar.WorldStreaming
{
    /// <summary>
    /// Dựng nhánh <c>StreamingWorld</c> bằng một hàm duy nhất.
    ///
    /// Trình dựng scene ở Editor và PlayMode test cùng gọi hàm này, nên cái được test chính là cái
    /// nằm trong scene — không có hai đường dựng để lệch nhau.
    /// </summary>
    public static class WorldStreamingRig
    {
        public const string RootName = "StreamingWorld";
        public const string ManagerName = "WorldStreamManager";
        public const string PoolName = "ChunkPool";
        public const string SurfaceName = "SharedGameplaySurface";

        /// <summary>Kết quả dựng — đủ tham chiếu để scene builder hoặc test đi tiếp.</summary>
        public readonly struct Rig
        {
            public readonly GameObject Root;
            public readonly WorldStreamManager Manager;
            public readonly ChunkPool Pool;
            public readonly SharedGameplaySurface Surface;

            public Rig(GameObject root, WorldStreamManager manager, ChunkPool pool, SharedGameplaySurface surface)
            {
                Root = root;
                Manager = manager;
                Pool = pool;
                Surface = surface;
            }
        }

        public static Rig Build(WorldStreamingConfig config, Transform streamingTarget, Material diagnosticMaterial, bool initializeOnStart) =>
            Build(config, streamingTarget, diagnosticMaterial, null, null, initializeOnStart);

        public static Rig Build(WorldStreamingConfig config, Transform streamingTarget, Material diagnosticMaterial,
            Material solidDecorMaterial, Material foliageMaterial, bool initializeOnStart)
        {
            if (config == null) throw new System.ArgumentNullException(nameof(config));

            var root = new GameObject(RootName);

            var managerGO = new GameObject(ManagerName);
            managerGO.transform.SetParent(root.transform, false);
            var manager = managerGO.AddComponent<WorldStreamManager>();

            var poolGO = new GameObject(PoolName);
            poolGO.transform.SetParent(root.transform, false);
            var pool = poolGO.AddComponent<ChunkPool>();
            pool.SetDiagnosticMaterial(diagnosticMaterial);
            pool.SetDecorationMaterials(solidDecorMaterial, foliageMaterial);
            pool.SetRootsParent(poolGO.transform);

            var surfaceGO = new GameObject(SurfaceName);
            surfaceGO.transform.SetParent(root.transform, false);
            var surface = surfaceGO.AddComponent<SharedGameplaySurface>();
            surface.Configure(config);

            manager.SetConfig(config);
            manager.SetPool(pool);
            manager.SetSurface(surface);
            manager.SetTarget(streamingTarget);
            manager.SetInitializeOnStart(initializeOnStart);

            return new Rig(root, manager, pool, surface);
        }
    }
}
