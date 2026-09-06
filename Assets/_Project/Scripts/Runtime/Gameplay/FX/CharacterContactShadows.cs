using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// MỘT mesh động, MỘT renderer, MỘT material cho bóng tiếp đất của NGƯỜI CHƠI và TOÀN BỘ quái.
    ///
    /// Lý do tồn tại, đo được chứ không phỏng đoán: kiến trúc cũ cho mỗi nhân vật một MeshRenderer
    /// riêng dùng material trong suốt (`M_BlobShadow`, queue 3000). Hình trong suốt được sắp xếp
    /// xa-tới-gần theo TỪNG OBJECT, nên Unity nộp mỗi bóng thành một draw riêng bất kể material đã bật
    /// GPU instancing. Đo trên frame đóng băng: 30 quái = 30 draw, đúng 1.00/con — đó chính là toàn bộ
    /// phần chi phí render còn tăng tuyến tính theo số quái. Tắt bóng đi thì cả stack quái tụt xuống
    /// +2 draw cho 30 con.
    ///
    /// Gộp tất cả vào MỘT mesh làm biến mất vấn đề sắp xếp: chỉ còn một object để sắp, nên còn một
    /// draw — không cần instancing, không cần indirect, không cần compute.
    ///
    /// Hệ này KHÔNG phải bóng thật và không cố giả vờ là bóng thật. Nó là vệt tiếp đất giúp nhân vật
    /// không bị trôi lơ lửng khi nhìn từ trên xuống.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]   // sau khi nhân vật đã di chuyển xong trong frame
    public sealed class CharacterContactShadows : MonoBehaviour
    {
        private const int VertsPerShadow = 4;
        private const int IndicesPerShadow = 6;

        [Tooltip("Material dùng chung. Bỏ trống = tự dựng từ shader ZombieWar/Environment/CharacterContactShadow.")]
        [SerializeField] private Material shadowMaterial;

        [Tooltip("Số bóng tối đa. Vượt quá là báo lỗi to, không âm thầm bỏ bóng của ai.")]
        [SerializeField] private int capacity = 160;

        [Tooltip("Độ dịch của vệt bóng ngược hướng đèn giả, mét. Giữ nhỏ để bóng không rời khỏi chân.")]
        [SerializeField] private float lightOffsetDistance = 0.12f;

        // Mặt gameplay PHẲNG ở Y=0; shader biome chỉ đổi màu chứ không dời hình học. Nhấc lên chỉ để
        // tránh đồng phẳng gây z-fighting, KHÔNG phải vì địa hình nhấp nhô — ghi chú cũ nói sai điều đó.
        [Tooltip("Nhấc vệt bóng khỏi mặt phẳng gameplay, mét. Chỉ để tránh z-fighting đồng phẳng.")]
        [SerializeField] private float groundLift = 0.01f;

        /// <summary>Một chỗ đăng ký. Không có GameObject, không có Renderer — chỉ là dữ liệu.</summary>
        private struct Entry
        {
            public Transform Target;
            public float HalfWidth;
            public float HalfLength;
            public float Opacity;
            public bool Visible;
            public float Fade;      // 0 = đậm đủ, 1 = tan hết
            public bool InUse;
        }

        private Entry[] _entries;
        private int[] _freeHandles;
        private int _freeCount;

        private Mesh _mesh;
        private MeshFilter _filter;
        private MeshRenderer _renderer;

        private Vector3[] _vertices;
        private Vector2[] _uvs;
        private Color32[] _colors;
        private int[] _indices;

        private Vector3 _offsetDirection = new Vector3(0.35f, 0f, -0.35f);

        public static CharacterContactShadows Instance { get; private set; }

        private int _playerHandle = -1;
        private Transform _playerTransform;

        [Header("Player")]
        [Tooltip("Bán kính vệt tiếp đất của người chơi.")]
        [SerializeField] private float playerShadowRadius = 0.42f;

        [Range(0f, 1f)]
        [SerializeField] private float playerShadowOpacity = 0.5f;

        /// <summary>
        /// Lấy (hoặc dựng) hệ bóng dùng chung.
        ///
        /// Tự dựng thay vì bắt mỗi map phải cắm sẵn một node: quái đăng ký ngay ở OnEnable, và một map
        /// quên cắm node sẽ làm CẢ ĐÀN mất bóng mà không có lỗi nào. Đúng một node cho mỗi scene
        /// gameplay, và nó tự dọn khi scene bị huỷ.
        /// </summary>
        public static CharacterContactShadows EnsureInstance()
        {
            if (Instance != null) return Instance;

            // Tìm CẢ object đang tắt.
            //
            // Quái đăng ký ở OnEnable, và thứ tự Awake giữa hai object không liên quan là không bảo
            // đảm: manager có thể chưa chạy Awake (nên Instance còn null) trong khi nó đã nằm sẵn
            // trong scene. Bản trước dùng FindFirstObjectByType (bỏ qua object tắt) rồi TỰ TẠO một
            // manager mới — hàng rào chống trùng lập tức huỷ bản thừa và ghi một lỗi đỏ mỗi lần.
            // Log của bản build WebGL bắt đúng vệt này.
            var found = FindObjectsByType<CharacterContactShadows>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (found.Length > 0) return Instance = found[0];

            // KHÔNG tự dựng nữa. Một manager dựng lúc chạy không có material tác giả, nên nó chỉ tạo
            // ra một hệ bóng câm lặng. Thiếu manager là lỗi lắp map và phải kêu to.
            Debug.LogError(
                "[ContactShadows] Map không có CharacterContactShadows. Cắm một object mang component " +
                "này vào scene gameplay và gán Assets/_Project/Art/Materials/M_CharacterContactShadow.mat.");
            return null;
        }

        public int Capacity => _entries != null ? _entries.Length : 0;
        public int RegisteredCount { get; private set; }
        public int VisibleShadowCount { get; private set; }
        public MeshRenderer Renderer => _renderer;
        public Mesh BatchMesh => _mesh;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Hai hệ bóng cùng lúc sẽ vẽ chồng và nhân đôi draw. Đây là lỗi lắp scene, phải nói ra.
                Debug.LogError("[ContactShadows] Đã có một CharacterContactShadows khác — huỷ bản thừa.", this);
                Destroy(this);
                return;
            }

            Instance = this;
            Build();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_mesh != null)
            {
                if (Application.isPlaying) Destroy(_mesh);
                else DestroyImmediate(_mesh);
                _mesh = null;
            }
        }

        private void Build()
        {
            if (capacity < 1) capacity = 1;

            _entries = new Entry[capacity];
            _freeHandles = new int[capacity];
            for (int i = 0; i < capacity; i++) _freeHandles[i] = capacity - 1 - i;
            _freeCount = capacity;

            _vertices = new Vector3[capacity * VertsPerShadow];
            _uvs = new Vector2[capacity * VertsPerShadow];
            _colors = new Color32[capacity * VertsPerShadow];
            _indices = new int[capacity * IndicesPerShadow];

            // UV và chỉ số tam giác là HẰNG SỐ — dựng đúng một lần, không bao giờ đụng lại.
            for (int i = 0; i < capacity; i++)
            {
                int v = i * VertsPerShadow;
                _uvs[v + 0] = new Vector2(0f, 0f);
                _uvs[v + 1] = new Vector2(1f, 0f);
                _uvs[v + 2] = new Vector2(1f, 1f);
                _uvs[v + 3] = new Vector2(0f, 1f);

                int t = i * IndicesPerShadow;
                _indices[t + 0] = v + 0;
                _indices[t + 1] = v + 2;
                _indices[t + 2] = v + 1;
                _indices[t + 3] = v + 0;
                _indices[t + 4] = v + 3;
                _indices[t + 5] = v + 2;
            }

            _mesh = new Mesh { name = "CharacterContactShadowBatch" };
            _mesh.MarkDynamic();
            _mesh.indexFormat = capacity * VertsPerShadow > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            _mesh.vertices = _vertices;
            _mesh.uv = _uvs;
            _mesh.colors32 = _colors;
            _mesh.triangles = _indices;

            _filter = gameObject.GetComponent<MeshFilter>();
            if (_filter == null) _filter = gameObject.AddComponent<MeshFilter>();
            _filter.sharedMesh = _mesh;

            _renderer = gameObject.GetComponent<MeshRenderer>();
            if (_renderer == null) _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = ResolveMaterial();

            // Bóng tiếp đất KHÔNG tham gia bất kỳ hợp đồng viền nào: bit 0 và chỉ bit 0.
            _renderer.renderingLayerMask = 1u;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            _renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            _renderer.allowOcclusionWhenDynamic = false;

            ResolveLightDirection();
        }

        private Material ResolveMaterial()
        {
            if (shadowMaterial != null) return shadowMaterial;

            // KHÔNG tạo material lúc chạy, và KHÔNG Shader.Find làm đường lùi.
            //
            // Cả hai đường đó đều hỏng THẦM LẶNG: một material dựng lúc chạy thiếu đúng thứ tác giả
            // đặt trên asset, và đó chính là cách bản đầu cho ra những mảng chữ nhật cứng mà không có
            // một dòng lỗi nào. Thiếu material là lỗi lắp đặt, và nó phải kêu to.
            Debug.LogError(
                "[ContactShadows] Chưa gán material dùng chung. Cắm một CharacterContactShadows vào map " +
                "và gán Assets/_Project/Art/Materials/M_CharacterContactShadow.mat. " +
                "Bóng tiếp đất sẽ KHÔNG được vẽ cho tới khi việc đó xong.", this);
            return null;
        }

        /// <summary>
        /// Dùng LẠI hướng đèn giả đang có của dự án thay vì bịa ra một hướng thứ hai.
        ///
        /// Nếu không có đèn hướng nào thì lùi về một hướng chéo cố định đã ghi rõ — bóng vẫn đúng chỗ,
        /// chỉ là không lệch theo đèn.
        /// </summary>
        private void ResolveLightDirection()
        {
            Light sun = RenderSettings.sun;
            if (sun == null)
            {
                foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                {
                    if (l.type == LightType.Directional && l.isActiveAndEnabled) { sun = l; break; }
                }
            }

            if (sun != null)
            {
                Vector3 flat = sun.transform.forward;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.0001f) _offsetDirection = flat.normalized;
            }
        }

        // --- đăng ký ------------------------------------------------------------------------

        /// <summary>Xin một chỗ. Trả về handle, hoặc -1 khi hết chỗ (kèm lỗi to).</summary>
        public int Register(Transform target, float halfWidth, float halfLength, float opacity)
        {
            if (target == null) return -1;

            if (_freeCount <= 0)
            {
                Debug.LogError(
                    $"[ContactShadows] Hết chỗ ({Capacity}). Nâng capacity hoặc kiểm tra rò đăng ký — " +
                    "KHÔNG âm thầm bỏ bóng của nhân vật.", this);
                return -1;
            }

            int handle = _freeHandles[--_freeCount];
            _entries[handle] = new Entry
            {
                Target = target,
                HalfWidth = Mathf.Max(0.01f, halfWidth),
                HalfLength = Mathf.Max(0.01f, halfLength),
                Opacity = Mathf.Clamp01(opacity),
                Visible = true,
                Fade = 0f,
                InUse = true,
            };

            RegisteredCount++;
            return handle;
        }

        /// <summary>Trả chỗ. Gọi hai lần trên cùng handle là vô hại.</summary>
        public void Unregister(int handle)
        {
            if (!IsValid(handle)) return;

            _entries[handle] = default;
            _freeHandles[_freeCount++] = handle;
            RegisteredCount--;
        }

        public void SetVisible(int handle, bool visible)
        {
            if (!IsValid(handle)) return;
            _entries[handle].Visible = visible;
        }

        /// <summary>0 = đậm đủ, 1 = tan hết. Dùng cho lúc quái tan xác.</summary>
        public void SetFade(int handle, float fade)
        {
            if (!IsValid(handle)) return;
            _entries[handle].Fade = Mathf.Clamp01(fade);
        }

        public void SetSize(int handle, float halfWidth, float halfLength)
        {
            if (!IsValid(handle)) return;
            _entries[handle].HalfWidth = Mathf.Max(0.01f, halfWidth);
            _entries[handle].HalfLength = Mathf.Max(0.01f, halfLength);
        }

        private bool IsValid(int handle) =>
            _entries != null && handle >= 0 && handle < _entries.Length && _entries[handle].InUse;

        // --- dựng mesh ----------------------------------------------------------------------

        /// <summary>
        /// Ghi lại toàn bộ bóng đang nhìn thấy được.
        ///
        /// Chạy ở LateUpdate với execution order 200, tức SAU khi nhân vật đã di chuyển xong, nên bóng
        /// không trễ một frame so với chân nhân vật.
        ///
        /// Đỉnh viết trong KHÔNG GIAN CỤC BỘ của node này, và node bám theo người chơi. Thế giới có thể
        /// đi rất xa gốc toạ độ; nếu ghi thẳng toạ độ world vào một mesh đứng yên ở gốc thì sai số dấu
        /// phẩy động sẽ làm bóng lệch dần khỏi chân.
        /// </summary>
        private void LateUpdate()
        {
            if (_entries == null) return;

            SyncPlayerRegistration();
            RecentreOnPlayer();
            RebuildNow();
        }

        /// <summary>
        /// Ghi lại mesh bóng. Tách khỏi <c>LateUpdate</c> để test đo được cấp phát của CHÍNH đường ghi
        /// này, thay vì đo heap của cả domain qua nhiều frame (phép đo đó gộp cả rác của test runner).
        /// </summary>
        internal void RebuildNow()
        {

            Matrix4x4 worldToLocal = transform.worldToLocalMatrix;
            Vector3 offset = _offsetDirection * lightOffsetDistance;

            int visible = 0;
            float minX = float.MaxValue, maxX = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            for (int i = 0; i < _entries.Length; i++)
            {
                ref Entry e = ref _entries[i];
                int v = i * VertsPerShadow;

                bool draw = e.InUse && e.Visible && e.Target != null && e.Fade < 1f;
                if (!draw)
                {
                    // Quad suy biến: bốn đỉnh trùng nhau => không có pixel nào bị tô, không cần đổi
                    // số chỉ số và không cần cấp phát lại buffer.
                    if (_vertices[v] != Vector3.zero)
                    {
                        _vertices[v] = _vertices[v + 1] = _vertices[v + 2] = _vertices[v + 3] = Vector3.zero;
                        _colors[v] = _colors[v + 1] = _colors[v + 2] = _colors[v + 3] = new Color32(0, 0, 0, 0);
                    }
                    continue;
                }

                // Nhấc lên một chút khỏi mặt đất. Mesh nền có nhấp nhô theo biome, nên một vệt bóng
                // đặt đúng y = 0 sẽ bị chính mặt đất nuốt từng mảng. Blob cũ cũng phải nhấc lên vì lý
                // do này (EnemyRosterTests đòi localPosition.y > 0).
                Vector3 world = e.Target.position + offset;
                world.y += groundLift;
                Vector3 c = worldToLocal.MultiplyPoint3x4(world);

                float hw = e.HalfWidth;
                float hl = e.HalfLength;

                _vertices[v + 0] = new Vector3(c.x - hw, c.y, c.z - hl);
                _vertices[v + 1] = new Vector3(c.x + hw, c.y, c.z - hl);
                _vertices[v + 2] = new Vector3(c.x + hw, c.y, c.z + hl);
                _vertices[v + 3] = new Vector3(c.x - hw, c.y, c.z + hl);

                byte a = (byte)(Mathf.Clamp01(e.Opacity * (1f - e.Fade)) * 255f);
                var col = new Color32(255, 255, 255, a);
                _colors[v + 0] = col; _colors[v + 1] = col; _colors[v + 2] = col; _colors[v + 3] = col;

                if (c.x - hw < minX) minX = c.x - hw;
                if (c.x + hw > maxX) maxX = c.x + hw;
                if (c.z - hl < minZ) minZ = c.z - hl;
                if (c.z + hl > maxZ) maxZ = c.z + hl;
                visible++;
            }

            VisibleShadowCount = visible;

            // SetVertices/SetColors với overload mảng: đây là đường KHÔNG cấp phát. Gán qua property
            // (`mesh.vertices = ...`) sao chép qua một mảng trung gian mỗi lần, và với 640 đỉnh mỗi
            // frame nó đủ sinh ra hàng trăm KB rác — test cấp phát bắt đúng chỗ này.
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colors);

            // Bounds tính từ chính các bóng đang hoạt động. Không gọi RecalculateBounds (nó quét lại
            // toàn bộ đỉnh, kể cả hàng trăm đỉnh suy biến ở gốc) và cũng không phồng bounds lên vô tội
            // vạ — làm thế là giết luôn frustum culling.
            _mesh.bounds = visible == 0
                ? new Bounds(Vector3.zero, Vector3.zero)
                : new Bounds(
                    new Vector3((minX + maxX) * 0.5f, 0f, (minZ + maxZ) * 0.5f),
                    new Vector3(Mathf.Max(0.1f, maxX - minX), 0.2f, Mathf.Max(0.1f, maxZ - minZ)));

            if (_renderer != null) _renderer.enabled = visible > 0;
        }

        /// <summary>
        /// Giữ ĐÚNG MỘT đăng ký cho người chơi, kể cả khi người chơi được sinh lại giữa hai lượt.
        ///
        /// So sánh theo Transform chứ không theo cờ "đã đăng ký chưa": sau khi về HOME rồi vào lượt
        /// mới, người chơi là một object KHÁC, và một cờ boolean sẽ khiến bóng bám vào cái xác cũ đã
        /// bị huỷ. So sánh transform làm việc đó tự lộ ra và tự sửa.
        /// </summary>
        private void SyncPlayerRegistration()
        {
            var player = PlayerMovement.Instance;
            Transform t = player != null ? player.transform : null;

            if (t == _playerTransform) return;

            if (_playerHandle >= 0) { Unregister(_playerHandle); _playerHandle = -1; }
            _playerTransform = t;

            if (t != null)
                _playerHandle = Register(t, playerShadowRadius, playerShadowRadius, playerShadowOpacity);
        }

        /// <summary>
        /// Giữ gốc mesh ở gần người chơi, nhảy theo từng bước lớn.
        ///
        /// Nhảy theo bước (chứ không bám mượt) để toạ độ cục bộ luôn nhỏ mà node không phải ghi
        /// transform mới mỗi frame.
        /// </summary>
        private void RecentreOnPlayer()
        {
            var player = PlayerMovement.Instance;
            if (player == null) return;

            Vector3 p = player.transform.position;
            Vector3 current = transform.position;
            if ((p - current).sqrMagnitude < 64f * 64f) return;

            transform.position = new Vector3(p.x, 0f, p.z);
        }
    }
}
