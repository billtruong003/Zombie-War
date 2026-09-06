using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZombieWar.WorldStreaming
{
    /// <summary>
    /// Một loại trang trí có thể xuất hiện trong thế giới.
    ///
    /// Một entry = một quyết định đặt. Nó có thể mang hai nguồn hình học (thân + tán) nhưng vẫn chỉ
    /// sinh ra MỘT placement — đó là điều giữ cho quyền sở hữu theo chunk không bị nhập nhằng.
    /// </summary>
    [Serializable]
    public class DecorationPaletteEntry
    {
        [Tooltip("ID ổn định. Đổi ID = đổi cả thế giới đã sinh ra. Không phụ thuộc vị trí trong danh sách.")]
        [SerializeField] private string stableId = "entry";

        [Tooltip("Hình học chính. Category của nó quyết định output nào nhận phần này.")]
        [SerializeField] private DecorationMeshSource primarySource;

        [Tooltip("Hình học phụ tuỳ chọn — ví dụ thân cây đi vào Solid trong khi tán lá đi vào Foliage.")]
        [SerializeField] private DecorationMeshSource secondarySource;

        [Header("Distribution")]
        [Tooltip("Cạnh ô lưới toàn cục, mét. Ô nhỏ = dày hơn.")]
        [SerializeField] private float cellSize = 2.5f;

        [Tooltip("Số ứng viên xét trong mỗi ô.")]
        [Range(1, 4)]
        [SerializeField] private int candidatesPerCell = 1;

        [Tooltip("Xác suất nền trước khi nhân với ái lực biome.")]
        [Range(0f, 1f)]
        [SerializeField] private float baseDensity = 0.5f;

        [Tooltip("Bán kính giãn cách, mét. > 0 sẽ bật luật loại trừ theo độ ưu tiên với các ô lân cận.")]
        [SerializeField] private float spacingRadius;

        [Header("Biome affinity (Dry, Grass, Sand, Rock)")]
        [Tooltip("Nhân với trọng số biome tương ứng rồi cộng lại thành hệ số mật độ.")]
        [SerializeField] private Vector4 biomeAffinity = new Vector4(0.2f, 1f, 0.1f, 0.05f);

        [Header("Transform")]
        [SerializeField] private Vector2 uniformScaleRange = new Vector2(0.85f, 1.25f);

        [Tooltip("Xoay ngẫu nhiên quanh trục Y trong khoảng [0,360).")]
        [SerializeField] private bool randomYaw = true;

        [Header("Appearance")]
        [Tooltip("Dải biến thiên tint ghi vào vertex color của mesh gộp.")]
        [SerializeField] private Vector2 tintRange = new Vector2(0.82f, 1.1f);

        [Tooltip("Ưu tiên khi ngân sách đỉnh bị vượt. Số lớn được giữ lại trước.")]
        [SerializeField] private int budgetPriority = 100;

        [Tooltip("Hệ số màu riêng của entry, nhân vào vertex color của mesh gộp. Trắng = giữ nguyên như cũ.")]
        [SerializeField] private Color foliageTint = Color.white;

        [Header("Wind (M4.6C.3)")]
        [Tooltip("Biên độ gió của loại này. 1 = cỏ mềm, 0.3 = bụi nặng, 0 = không đu đưa.")]
        [Range(0f, 2f)]
        [SerializeField] private float windAmplitude = 1f;

        [Header("Distance density (M3B.2D)")]
        [Tooltip("Cho phép giảm mật độ ở vòng ngoài. CHỈ bật cho cây cỏ nhỏ, lặp lại nhiều, không có ý nghĩa gameplay.")]
        [SerializeField] private bool distanceDensityEligible;

        [Header("Clumping (M4.6B)")]
        [Tooltip("Bước sóng của trường vón cụm, mét. 0 = tắt, giữ nguyên phân bố cũ.")]
        [SerializeField] private float clumpCellSize;

        [Tooltip("Độ mạnh vón cụm. 0 = rải đều như cũ, 1 = mảng dày xen mảng trống.")]
        [Range(0f, 1f)]
        [SerializeField] private float clumpStrength;

        public string StableId => stableId;
        public DecorationMeshSource PrimarySource => primarySource;
        public DecorationMeshSource SecondarySource => secondarySource;
        public float CellSize => cellSize;
        public int CandidatesPerCell => candidatesPerCell;
        public float BaseDensity => baseDensity;
        public float SpacingRadius => spacingRadius;
        public Vector4 BiomeAffinity => biomeAffinity;
        public Vector2 UniformScaleRange => uniformScaleRange;
        public bool RandomYaw => randomYaw;
        public Vector2 TintRange => tintRange;
        public int BudgetPriority => budgetPriority;

        /// <summary>
        /// Hệ số màu riêng cho từng entry, nhân vào vertex color khi gộp mesh.
        ///
        /// Sáu entry cây cỏ dùng CHUNG một ô atlas và CHUNG một vật liệu, nên trước M4.6C không có
        /// chỗ nào đặt được màu riêng cho từng loại: đổi vật liệu là đổi tất cả, đổi atlas cũng vậy.
        /// Vertex color là kênh duy nhất còn lại mà không phải tách vật liệu hay thêm submesh.
        ///
        /// Trắng là mặc định và cho ra ĐÚNG byte như trước — vì trước đây vertex color vốn đã là
        /// `(t,t,t)` và nhân với trắng không đổi gì. Nhờ vậy mọi bảng dữ liệu cũ giữ nguyên kết quả.
        /// </summary>
        public Color FoliageTint => foliageTint;

        /// <summary>
        /// Biên độ gió của loại cây này, ghi vào UV1.y của mesh gộp.
        ///
        /// Cỏ phải mềm hơn bụi rậm, nhưng KHÔNG được rẽ nhánh theo tên prefab lúc chạy — đó là dữ
        /// liệu, nên nó nằm trong palette. Mặc định 1 để mọi bảng dữ liệu cũ vẫn hợp lệ.
        /// </summary>
        public float WindAmplitude => windAmplitude;

        /// <summary>
        /// Entry này có được phép giảm mật độ ở vòng ngoài hay không.
        ///
        /// Mặc định FALSE, tức là phải khai báo tường minh mới bị giảm. Chọn mặc định như vậy vì hậu
        /// quả hai chiều không cân nhau: bỏ sót một bụi cỏ chỉ là mất một chút hiệu năng, còn vô tình
        /// làm biến mất một cái cây, tảng đá hay POI là làm hỏng thế giới.
        ///
        /// Việc phân loại nằm ở đây — dữ liệu của project — chứ không suy từ tên prefab hay tên file
        /// lúc chạy, vì tên là thứ đổi được mà không ai nhận ra hệ quả.
        /// </summary>
        public bool DistanceDensityEligible => distanceDensityEligible;

        /// <summary>Muối suy từ ID chứ không từ chỉ số — sắp xếp lại danh sách không đổi thế giới.</summary>
        public int Salt => DecorationHash.SaltFromId(stableId);

        public bool UsesSpacing => spacingRadius > 0.0001f;

        public float ClumpCellSize => clumpCellSize;
        public float ClumpStrength => clumpStrength;

        /// <summary>Entry này có bật vón cụm hay không. Tắt thì mọi phép tính vón cụm bị bỏ qua hoàn toàn.</summary>
        public bool UsesClumping => clumpCellSize > 0.0001f && clumpStrength > 0.0001f;

        /// <summary>
        /// Hệ số vón cụm tại một điểm toàn cục. Bằng đúng <c>1</c> khi tắt.
        ///
        /// Lưới ô có jitter cho ra phân bố ĐỀU HƠN cả ngẫu nhiên: mỗi ô đúng một ứng viên, và xác suất
        /// nhận chỉ phụ thuộc biome — thứ biến thiên rất chậm. Kết quả là cỏ trải như rắc muối, không
        /// bao giờ thành mảng. Chỗ này thêm một trường tần số thấp nhân vào mật độ, nên vùng nào cũng
        /// dày lên hoặc thưa đi CÙNG NHAU thay vì quyết định độc lập từng ô.
        ///
        /// Dùng lại <see cref="BiomeSampler.ValueNoise"/> chứ không tự viết noise mới: nó liên tục nên
        /// không tạo đường nối ở biên chunk, nó thuần toạ độ toàn cục nên chunk nào hỏi cũng ra một kết
        /// quả, và nó đã có sẵn bảo đảm tất định của trường biome.
        ///
        /// Đường cong phải BÌNH PHƯƠNG smoothstep chứ không dùng thẳng. Dùng thẳng thì hệ số ở vùng
        /// trũng vẫn còn khoảng <c>0.15</c>, tức là chỗ nào cũng còn cỏ và mắt không đọc ra mảng — đo
        /// lần đầu đúng như vậy, tỷ lệ vón cụm gần như không nhúc nhích. Bình phương kéo vùng trũng về
        /// gần <c>0</c> nên mới thực sự có khoảng trống giữa các mảng.
        ///
        /// Kỳ vọng của <c>smoothstep²</c> trên biến ngẫu nhiên đều là <c>0.371</c>, nên nhân
        /// <c>2.7</c> đưa trung bình về <c>≈1.00</c>: cỏ DỒN LẠI chứ không mọc thêm, và tổng số đỉnh
        /// gần như đứng yên trong khi hình ảnh đổi hẳn.
        /// </summary>
        public float ClumpFactorAt(int worldSeed, float worldX, float worldZ)
        {
            if (!UsesClumping) return 1f;

            float n = BiomeSampler.ValueNoise(worldSeed, worldX / clumpCellSize, worldZ / clumpCellSize, ClumpSalt);
            float shaped = n * n * (3f - 2f * n);
            return Mathf.Lerp(1f, shaped * shaped * MeanPreservingGain, clumpStrength);
        }

        /// <summary>1 / E[smoothstep²] — giữ mật độ trung bình không đổi khi bật vón cụm.</summary>
        private const float MeanPreservingGain = 2.7f;

        /// <summary>Muối riêng cho trường vón cụm. Đổi giá trị này là đổi mọi thế giới đã sinh.</summary>
        private const int ClumpSalt = 0x434C;

        /// <summary>Entry này có đóng góp hình học vào output Solid hay không.</summary>
        public bool HasSolidGeometry =>
            (primarySource != null && primarySource.Category == DecorationCategory.Solid) ||
            (secondarySource != null && secondarySource.Category == DecorationCategory.Solid);

        public int VertexCost =>
            (primarySource != null ? primarySource.VertexCount : 0) +
            (secondarySource != null ? secondarySource.VertexCount : 0);

        /// <summary>Hệ số mật độ tại một mẫu biome. Luôn ở <c>[0,1]</c>.</summary>
        public float DensityAt(in BiomeSample sample)
        {
            float affinity = biomeAffinity.x * sample.Dry
                             + biomeAffinity.y * sample.Grass
                             + biomeAffinity.z * sample.Sand
                             + biomeAffinity.w * sample.Rock;

            return Mathf.Clamp01(baseDensity * Mathf.Max(0f, affinity));
        }

        /// <summary>
        /// Ghi toàn bộ dữ liệu của entry. Dùng bởi công cụ authoring ở Editor và bởi test — runtime
        /// chỉ đọc.
        /// </summary>
        public void Configure(string id, DecorationMeshSource primary, DecorationMeshSource secondary,
            float cell, int candidates, float density, float spacing, Vector4 affinity,
            Vector2 scaleRange, Vector2 tint, int priority, bool yaw = true,
            bool densityEligible = false, float clumpCell = 0f, float clumpAmount = 0f,
            Color? foliageColor = null, float wind = 1f)
        {
            // Mặc định 1 = cỏ mềm, đúng giá trị mọi entry đang mang trước M4.6CD, nên các lời gọi cũ
            // vẫn cho ra dữ liệu y hệt.
            windAmplitude = wind;
            // Vón cụm mặc định TẮT, nên mọi lời gọi Configure có sẵn giữ nguyên kết quả từng bit.
            clumpCellSize = clumpCell;
            clumpStrength = clumpAmount;
            // Trắng là trung tính: nhân vào vertex color không đổi một byte nào.
            foliageTint = foliageColor ?? Color.white;
            distanceDensityEligible = densityEligible;
            stableId = id;
            primarySource = primary;
            secondarySource = secondary;
            cellSize = cell;
            candidatesPerCell = candidates;
            baseDensity = density;
            spacingRadius = spacing;
            biomeAffinity = affinity;
            uniformScaleRange = scaleRange;
            tintRange = tint;
            budgetPriority = priority;
            randomYaw = yaw;
        }

        public bool Validate(out string error)
        {
            if (string.IsNullOrWhiteSpace(stableId))
            {
                error = "Một entry thiếu stableId.";
                return false;
            }

            if (primarySource == null)
            {
                error = $"{stableId}: thiếu primarySource.";
                return false;
            }

            if (!primarySource.Validate(out string sourceError))
            {
                error = $"{stableId}: {sourceError}";
                return false;
            }

            if (secondarySource != null)
            {
                if (!secondarySource.Validate(out string secondaryError))
                {
                    error = $"{stableId}: {secondaryError}";
                    return false;
                }

                if (secondarySource.Category == primarySource.Category)
                {
                    error = $"{stableId}: hai nguồn cùng category ({primarySource.Category}) — " +
                            "nguồn phụ tồn tại để tách hình học sang output còn lại.";
                    return false;
                }
            }

            if (cellSize <= 0.01f)
            {
                error = $"{stableId}: cellSize phải > 0.01 (đang là {cellSize}).";
                return false;
            }

            if (baseDensity < 0f || baseDensity > 1f)
            {
                error = $"{stableId}: baseDensity phải trong [0,1] (đang là {baseDensity}).";
                return false;
            }

            if (uniformScaleRange.x <= 0f || uniformScaleRange.y < uniformScaleRange.x)
            {
                error = $"{stableId}: uniformScaleRange không hợp lệ ({uniformScaleRange}).";
                return false;
            }

            if (spacingRadius < 0f)
            {
                error = $"{stableId}: spacingRadius không được âm.";
                return false;
            }

            if (clumpCellSize < 0f)
            {
                error = $"{stableId}: clumpCellSize không được âm.";
                return false;
            }

            if (clumpStrength < 0f || clumpStrength > 1f)
            {
                error = $"{stableId}: clumpStrength phải trong [0,1] (đang là {clumpStrength}).";
                return false;
            }

            // Chốt chặn cứng: bất cứ entry nào đóng góp hình học Solid đều KHÔNG được giảm mật độ.
            // Solid là thân cây, đá, khúc gỗ — những thứ định hình bố cục và có thể mang ý nghĩa
            // gameplay. Đánh dấu nhầm một entry như vậy sẽ làm cây biến mất ở vòng ngoài, nên chỗ này
            // báo lỗi thay vì tin vào sự cẩn thận của người điền dữ liệu.
            if (distanceDensityEligible && HasSolidGeometry)
            {
                error = $"{stableId}: đã bật distanceDensityEligible nhưng entry có hình học Solid — " +
                        "mật độ theo khoảng cách chỉ áp cho cây cỏ nhỏ thuần Foliage.";
                return false;
            }

            error = null;
            return true;
        }
    }

    /// <summary>
    /// Danh sách các loại trang trí được duyệt cho MVP.
    ///
    /// Cố ý nhỏ. Mỗi entry thêm vào là thêm chi phí đỉnh trong mọi chunk, và mục tiêu M2B là chứng
    /// minh đường ống bake mesh gộp chứ không phải nhét hết cả pack vào thế giới.
    /// </summary>
    [CreateAssetMenu(menuName = "ZombieWar/World Streaming/Decoration Palette", fileName = "DecorationPalette")]
    public class DecorationPalette : ScriptableObject
    {
        [SerializeField] private List<DecorationPaletteEntry> entries = new List<DecorationPaletteEntry>();

        [Header("Budgets")]
        [Tooltip("Trần đỉnh cho mesh Solid gộp của một chunk.")]
        [SerializeField] private int solidVertexBudget = 15000;

        [Tooltip("Trần đỉnh cho mesh Foliage gộp của một chunk.")]
        [SerializeField] private int foliageVertexBudget = 45000;

        [Tooltip("Biên an toàn tuyệt đối của UInt16. Vượt qua đây là lỗi, không phải chuyện im lặng.")]
        [SerializeField] private int hardVertexCeiling = 60000;

        [Tooltip("Biên bounds thêm vào cho foliage, chừa chỗ cho wind sway của M3.")]
        [SerializeField] private float foliageBoundsPadding = 0.5f;

        public IReadOnlyList<DecorationPaletteEntry> Entries => entries;
        public int SolidVertexBudget => solidVertexBudget;
        public int FoliageVertexBudget => foliageVertexBudget;
        public int HardVertexCeiling => hardVertexCeiling;
        public float FoliageBoundsPadding => foliageBoundsPadding;

        /// <summary>Bán kính ô lớn nhất — quyết định phải quét bao nhiêu ô lân cận cho luật giãn cách.</summary>
        public float MaxSpacingRadius
        {
            get
            {
                float max = 0f;
                for (int i = 0; i < entries.Count; i++) max = Mathf.Max(max, entries[i].SpacingRadius);
                return max;
            }
        }

        /// <summary>Chỉ dùng cho test: dựng palette trong bộ nhớ.</summary>
        public static DecorationPalette CreateRuntime(List<DecorationPaletteEntry> runtimeEntries,
            int solidBudget = 15000, int foliageBudget = 45000)
        {
            var palette = CreateInstance<DecorationPalette>();
            palette.entries = runtimeEntries ?? new List<DecorationPaletteEntry>();
            palette.solidVertexBudget = solidBudget;
            palette.foliageVertexBudget = foliageBudget;
            return palette;
        }

        public void SetEntriesForTests(List<DecorationPaletteEntry> value) => entries = value;

        /// <summary>
        /// Ghi ngân sách đỉnh. Chỉ dùng bởi công cụ authoring ở Editor và bởi test — runtime chỉ đọc.
        ///
        /// Ngân sách phải nằm trong asset thay vì trong code vì nó là quyết định NỘI DUNG: nó thay đổi
        /// khi bảng màu thay đổi. M4.6CD.1 nâng trần Solid từ 15.000 lên 45.000 vì cảnh vật rắn đổi từ
        /// những khối 143 đỉnh sang cụm đá và đống phế liệu 1.194–3.372 đỉnh.
        /// </summary>
        public void SetBudgets(int solid, int foliage, int ceiling)
        {
            solidVertexBudget = solid;
            foliageVertexBudget = foliage;
            hardVertexCeiling = ceiling;
        }

        /// <summary>
        /// Kiểm tra toàn bộ palette. Trùng ID hoặc trùng muối là lỗi nặng: hai entry cùng muối sẽ
        /// đọc chung một luồng băm và mọc chồng lên nhau ở đúng cùng một chỗ.
        /// </summary>
        public bool Validate(out string error)
        {
            if (entries == null || entries.Count == 0)
            {
                error = "Palette rỗng.";
                return false;
            }

            var ids = new HashSet<string>();
            var salts = new HashSet<int>();

            for (int i = 0; i < entries.Count; i++)
            {
                DecorationPaletteEntry entry = entries[i];
                if (entry == null)
                {
                    error = $"Entry {i} là null.";
                    return false;
                }

                if (!entry.Validate(out string entryError))
                {
                    error = entryError;
                    return false;
                }

                if (!ids.Add(entry.StableId))
                {
                    error = $"Trùng stableId '{entry.StableId}'.";
                    return false;
                }

                if (!salts.Add(entry.Salt))
                {
                    error = $"Trùng muối ở '{entry.StableId}' — hai entry sẽ sinh ra cùng một vị trí.";
                    return false;
                }

                if (entry.VertexCost > hardVertexCeiling)
                {
                    error = $"'{entry.StableId}' tốn {entry.VertexCost} đỉnh, vượt trần {hardVertexCeiling}.";
                    return false;
                }
            }

            if (solidVertexBudget <= 0 || foliageVertexBudget <= 0)
            {
                error = "Ngân sách đỉnh phải > 0.";
                return false;
            }

            if (solidVertexBudget > hardVertexCeiling || foliageVertexBudget > hardVertexCeiling)
            {
                error = $"Ngân sách vượt trần UInt16 {hardVertexCeiling}.";
                return false;
            }

            error = null;
            return true;
        }
    }
}
