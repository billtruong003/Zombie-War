using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ZombieWar.WorldStreaming
{
    /// <summary>
    /// Hiển thị chẩn đoán cho lab: biên chunk, nhãn (x,z) + slot pool, và bảng đếm.
    ///
    /// Cố ý dùng Gizmos + Handles thay vì sinh GameObject nhãn cho từng chunk — nếu debug view tự tạo
    /// object mỗi lần recycle thì chính nó sẽ làm hỏng bài kiểm tra pooling mà nó phục vụ.
    ///
    /// M1: mesh nền đã phủ kín 32×32 m nên biên chunk chỉ còn nhìn thấy qua Gizmo ở đây.
    /// </summary>
    [DisallowMultipleComponent]
    public class WorldStreamingDebugView : MonoBehaviour
    {
        [SerializeField] private WorldStreamManager manager;

        [Header("Scene view")]
        [SerializeField] private bool drawBorders = true;
        [SerializeField] private bool drawLabels = true;
        [SerializeField] private Color borderColor = new Color(1f, 1f, 1f, 0.55f);
        [SerializeField] private Color centerColor = new Color(1f, 0.85f, 0.2f, 1f);

        [Header("Game view")]
        [SerializeField] private bool drawOverlay = true;

        [Header("Ground display")]
        [Tooltip("Textured = mặt đất thật. Hai chế độ còn lại soi thẳng dữ liệu biome bên dưới.")]
        [SerializeField] private GroundDisplayMode displayMode = GroundDisplayMode.Textured;

        [SerializeField] private KeyCode displayToggleKey = KeyCode.B;

        private readonly StringBuilder _overlay = new StringBuilder(1024);
        private Material _debugMaterial;
        private float _originalDebugMode;
        private bool _hasOriginalDebugMode;
        private GUIStyle _overlayStyle;
        private bool _pushedMode;

        public void SetManager(WorldStreamManager value) => manager = value;

        public GroundDisplayMode DisplayMode
        {
            get => displayMode;
            set
            {
                displayMode = value;
                PushDisplayMode();
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(displayToggleKey))
                DisplayMode = (GroundDisplayMode)(((int)displayMode + 1) % 3);

            if (!_pushedMode) PushDisplayMode();
        }

        /// <summary>
        /// Đẩy chế độ hiển thị lên đúng material dùng chung và khôi phục giá trị gốc khi component
        /// tắt. Không dùng MaterialPropertyBlock vì nó loại renderer khỏi SRP Batcher trong URP.
        /// </summary>
        private void PushDisplayMode()
        {
            if (manager == null || manager.Pool == null || !manager.Pool.IsPrewarmed) return;

            IReadOnlyList<ChunkInstance> instances = manager.Pool.All;
            Material material = instances.Count > 0 && instances[0].GroundRenderer != null
                ? instances[0].GroundRenderer.sharedMaterial
                : null;
            if (material == null || !material.HasProperty(ChunkDiagnosticAssets.DebugModeId)) return;

            if (_debugMaterial != material)
            {
                RestoreOriginalDebugMode();
                _debugMaterial = material;
                _originalDebugMode = material.GetFloat(ChunkDiagnosticAssets.DebugModeId);
                _hasOriginalDebugMode = true;
            }

            material.SetFloat(ChunkDiagnosticAssets.DebugModeId, (float)displayMode);

            for (int i = 0; i < instances.Count; i++)
            {
                MeshRenderer renderer = instances[i].GroundRenderer;
                if (renderer != null && renderer.HasPropertyBlock()) renderer.SetPropertyBlock(null);
            }

            _pushedMode = true;
        }

        private void OnDisable()
        {
            RestoreOriginalDebugMode();
            _pushedMode = false;
        }

        private void RestoreOriginalDebugMode()
        {
            if (_hasOriginalDebugMode && _debugMaterial != null)
                _debugMaterial.SetFloat(ChunkDiagnosticAssets.DebugModeId, _originalDebugMode);

            _debugMaterial = null;
            _hasOriginalDebugMode = false;
        }

        /// <summary>Bản tóm tắt trạng thái dùng chung cho overlay, log và test.</summary>
        public string BuildReport()
        {
            _overlay.Length = 0;

            if (manager == null || !manager.IsInitialized)
            {
                _overlay.Append("WorldStreamManager chưa khởi tạo.");
                return _overlay.ToString();
            }

            WorldStreamingConfig config = manager.Config;
            ChunkPool pool = manager.Pool;
            Transform target = manager.StreamingTarget;

            _overlay.Append("WORLD STREAMING LAB — M2B\n");
            _overlay.Append("seed ").Append(config.WorldSeed)
                    .Append("   chunk ").Append(config.ChunkSize).Append(" m")
                    .Append("   ring ").Append(config.ActiveDiameter).Append('×').Append(config.ActiveDiameter)
                    .Append('\n');
            _overlay.Append("ground ").Append(config.GroundResolution).Append('×').Append(config.GroundResolution)
                    .Append("   verts ").Append(config.GroundVertexCount)
                    .Append("   tris ").Append(config.GroundTriangleCount)
                    .Append('\n');

            if (target != null)
            {
                Vector3 p = target.position;
                _overlay.Append("probe  ").Append(p.x.ToString("F1")).Append(", ").Append(p.z.ToString("F1")).Append('\n');
                _overlay.Append("chunk  ").Append(manager.CurrentChunk).Append('\n');

                BiomeSample sample = BiomeSampler.Sample(config.WorldSeed, p.x, p.z);
                _overlay.Append("biome  ").Append(sample).Append('\n');
                _overlay.Append("domin. ").Append(sample.DominantName)
                        .Append("   sum ").Append(sample.WeightSum.ToString("F3")).Append('\n');
            }
            else
            {
                _overlay.Append("chunk  ").Append(manager.CurrentChunk).Append('\n');
            }

            _overlay.Append("display ").Append(displayMode).Append('\n');

            Material groundMaterial = pool != null && pool.All.Count > 0 && pool.All[0].GroundRenderer != null
                ? pool.All[0].GroundRenderer.sharedMaterial
                : null;
            _overlay.Append("material ").Append(groundMaterial != null ? groundMaterial.name : "none")
                    .Append("  shader ").Append(groundMaterial != null && groundMaterial.shader != null
                        ? groundMaterial.shader.name
                        : "none")
                    .Append('\n');

            AppendDecorationReport(pool);

            _overlay.Append("active leases      ").Append(manager.ActiveLeaseCount)
                    .Append(" / required ").Append(config.ActiveChunkCount).Append('\n');
            _overlay.Append("last refresh       in ").Append(manager.LastIncomingCount)
                    .Append("  out ").Append(manager.LastOutgoingCount).Append('\n');
            _overlay.Append("refreshes          ").Append(manager.RefreshCount).Append('\n');

            // M3B.2: hàng đợi sinh trang trí. Overlay chỉ ĐỌC các bộ đếm này — không có nhánh nào ở đây
            // được phép tác động ngược vào thứ tự hay ngân sách của bộ lập lịch.
            _overlay.Append("decor queue        ").Append(manager.PendingDecorationCount)
                    .Append(" pending   settled ").Append(manager.IsGenerationSettled ? "yes" : "NO").Append('\n');
            _overlay.Append("decor jobs         ").Append(manager.LastFrameCompletedDecorationJobs)
                    .Append(" this frame  / ").Append(config.DecorationJobsPerFrame).Append(" budget\n");
            if (config.DistanceDensityEnabled)
            {
                int near = 0, outer = 0, mismatched = 0;
                foreach (System.Collections.Generic.KeyValuePair<ChunkCoord, ChunkInstance> lease in manager.ActiveLeases)
                {
                    if (lease.Value.BuiltDecorationTier == DecorationDensityTier.Near) near++;
                    else outer++;
                    if (lease.Value.BuiltDecorationTier != lease.Value.DecorationTier) mismatched++;
                }

                _overlay.Append("density tiers      near ").Append(near).Append('/').Append(config.NearChunkCount)
                        .Append("  outer ").Append(outer).Append('/').Append(config.OuterChunkCount)
                        .Append("  awaiting retier ").Append(mismatched).Append('\n');
                _overlay.Append("density r/near/out ").Append(config.NearDensityRadius)
                        .Append(" / ").Append(config.NearFoliageDensity.ToString("F2"))
                        .Append(" / ").Append(config.OuterFoliageDensity.ToString("F2")).Append('\n');
            }

            _overlay.Append("decor totals       done ").Append(manager.CompletedDecorationJobs)
                    .Append("  stale ").Append(manager.CancelledStaleDecorationJobs)
                    .Append("  peak depth ").Append(manager.MaxObservedDecorationQueueDepth).Append('\n');

            if (pool != null)
            {
                _overlay.Append("pool capacity      ").Append(pool.Capacity).Append('\n');
                _overlay.Append("recycles           ").Append(pool.RecycleCount).Append('\n');
                _overlay.Append("roots after warmup ").Append(pool.RootsCreatedSinceWarmup).Append('\n');
                _overlay.Append("meshes after warmup ").Append(pool.GroundMeshesCreatedSinceWarmup).Append('\n');
                _overlay.Append("roots destroyed    ").Append(pool.RootsDestroyedDuringTraversal).Append('\n');
            }

            _overlay.Append("\n[WASD] move  [Shift] boost  [T]/[Y] teleport  [R] reset  [B] display mode");
            return _overlay.ToString();
        }

        /// <summary>
        /// Tong hop trang tri cua ca ring va cua rieng chunk trung tam.
        ///
        /// Doc thang tu 25 ChunkInstance, khong tao mot GameObject debug nao — chinh vi the no khong
        /// lam hong bai kiem tra pooling ma no dang phuc vu.
        /// </summary>
        private void AppendDecorationReport(ChunkPool pool)
        {
            if (pool == null || !pool.IsPrewarmed) return;

            IReadOnlyList<ChunkInstance> instances = pool.All;
            int placements = 0, solidVerts = 0, solidTris = 0, foliageVerts = 0, foliageTris = 0;
            int budgetRejected = 0, spacingRejected = 0, densityRejected = 0, ownershipRejected = 0, candidates = 0;
            int solidRenderers = 0, foliageRenderers = 0, propObjects = 0;
            float generateMs = 0f, applyMs = 0f;

            for (int i = 0; i < instances.Count; i++)
            {
                ChunkInstance chunk = instances[i];
                if (!chunk.IsAssigned) continue;

                placements += chunk.PlacementCount;
                solidVerts += chunk.SolidVertexCount;
                solidTris += chunk.SolidTriangleCount;
                foliageVerts += chunk.FoliageVertexCount;
                foliageTris += chunk.FoliageTriangleCount;
                generateMs += chunk.LastGenerateMs;
                applyMs += chunk.LastApplyMs;

                DecorationSampleStats stats = chunk.DecorationStats;
                candidates += stats.Candidates;
                densityRejected += stats.RejectedByDensity;
                spacingRejected += stats.RejectedBySpacing;
                ownershipRejected += stats.RejectedByOwnership;
                budgetRejected += stats.RejectedByBudget;

                if (chunk.SolidDecorRenderer != null && chunk.SolidDecorRenderer.enabled) solidRenderers++;
                if (chunk.FoliageRenderer != null && chunk.FoliageRenderer.enabled) foliageRenderers++;

                // Trang tri la du lieu: chunk root chi duoc phep co dung 3 cay con render.
                propObjects += chunk.transform.childCount - 3;
            }

            ChunkInstance center = null;
            for (int i = 0; i < instances.Count; i++)
                if (instances[i].IsAssigned && instances[i].Coord == manager.CurrentChunk) center = instances[i];

            _overlay.Append("placements ring    ").Append(placements);
            if (center != null) _overlay.Append("   centre ").Append(center.PlacementCount);
            _overlay.Append('\n');

            _overlay.Append("solid  v/t         ").Append(solidVerts).Append(" / ").Append(solidTris);
            if (center != null) _overlay.Append("   centre ").Append(center.SolidVertexCount);
            _overlay.Append('\n');

            _overlay.Append("foliage v/t        ").Append(foliageVerts).Append(" / ").Append(foliageTris);
            if (center != null) _overlay.Append("   centre ").Append(center.FoliageVertexCount);
            _overlay.Append('\n');

            _overlay.Append("candidates         ").Append(candidates)
                    .Append("  rej d/s/o/b ").Append(densityRejected).Append('/').Append(spacingRejected)
                    .Append('/').Append(ownershipRejected).Append('/').Append(budgetRejected).Append('\n');

            _overlay.Append("decor renderers    solid ").Append(solidRenderers)
                    .Append("  foliage ").Append(foliageRenderers)
                    .Append("  prop GOs ").Append(propObjects).Append('\n');

            _overlay.Append("decor gen/apply ms ").Append(generateMs.ToString("F2"))
                    .Append(" / ").Append(applyMs.ToString("F2")).Append('\n');

            _overlay.Append("decor meshes new   ").Append(pool.DecorationMeshesCreatedSinceWarmup).Append('\n');
        }

        private void OnGUI()
        {
            if (!drawOverlay || manager == null) return;

            _overlayStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.UpperLeft,
                richText = false,
            };

            GUI.Box(new Rect(10f, 10f, 460f, 480f), GUIContent.none);
            GUI.Label(new Rect(20f, 18f, 450f, 470f), BuildReport(), _overlayStyle);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (manager == null || !manager.IsInitialized || manager.Config == null) return;
            if (!drawBorders && !drawLabels) return;

            float size = manager.Config.ChunkSize;
            ChunkCoord center = manager.CurrentChunk;

            foreach (KeyValuePair<ChunkCoord, ChunkInstance> lease in manager.ActiveLeases)
            {
                ChunkCoord coord = lease.Key;
                Vector3 min = coord.ToWorldMin(size);
                bool isCenter = coord == center;

                if (drawBorders)
                {
                    Gizmos.color = isCenter ? centerColor : borderColor;
                    Vector3 a = min + Vector3.up * 0.05f;
                    Vector3 b = a + new Vector3(size, 0f, 0f);
                    Vector3 c = a + new Vector3(size, 0f, size);
                    Vector3 d = a + new Vector3(0f, 0f, size);
                    Gizmos.DrawLine(a, b);
                    Gizmos.DrawLine(b, c);
                    Gizmos.DrawLine(c, d);
                    Gizmos.DrawLine(d, a);
                }

                if (drawLabels)
                {
                    UnityEditor.Handles.color = isCenter ? centerColor : Color.white;
                    UnityEditor.Handles.Label(
                        coord.ToWorldCenter(size) + Vector3.up * 0.2f,
                        $"{coord}\nslot {lease.Value.SlotId:D2}");
                }
            }

            Transform target = manager.StreamingTarget;
            if (target != null)
            {
                Gizmos.color = centerColor;
                Gizmos.DrawWireSphere(target.position, 1.5f);
            }
        }
#endif
    }
}
