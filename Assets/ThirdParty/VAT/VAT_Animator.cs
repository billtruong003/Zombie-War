using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer))] // Phải là MeshRenderer, không phải SkinnedMeshRenderer
public class VAT_Animator : MonoBehaviour
{
    public VAT_AnimationData animationData;
    public float playbackSpeed = 1.0f;

    [Tooltip("Animation clip to play on Start.")]
    public int defaultClipIndex = 0;

    [Header("Visualization")]
    [Tooltip("If checked, draws a gizmo in the Scene View showing the total animation bounds.")]
    public bool showAnimationBounds = true;

    private Renderer _renderer;
    private MaterialPropertyBlock _propertyBlock;

    private VAT_AnimationData.ClipInfo _currentClip;
    private VAT_AnimationData.ClipInfo _previousClip;

    private float _currentTimeSeconds;
    private float _previousTimeSeconds;
    private float _crossFadeTimer;
    private float _crossFadeDuration;
    private bool _isBlending;

    private static readonly int CurrentTimeID = Shader.PropertyToID("_CurrentAnimNormalizedTime");
    private static readonly int PreviousTimeID = Shader.PropertyToID("_PreviousAnimNormalizedTime");
    private static readonly int BlendWeightID = Shader.PropertyToID("_AnimationBlendWeight");
    private static readonly int PositionTexID = Shader.PropertyToID("_PositionTexture");
    private static readonly int PositionMinID = Shader.PropertyToID("_PositionMin");
    private static readonly int PositionMaxID = Shader.PropertyToID("_PositionMax");

#if UNITY_EDITOR
    private double _lastEditorUpdateTime;
#endif

    private void OnEnable()
    {
        Initialize();
#if UNITY_EDITOR
        EditorApplication.update += EditorTick;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorTick;
#endif
    }

    private void Update()
    {
        if (Application.isPlaying)
        {
            Tick(Time.deltaTime);
        }
    }

    private void OnValidate()
    {
        // Được gọi khi có thay đổi trong Inspector
        if (this.isActiveAndEnabled)
        {
            Initialize();
        }
    }

    private void Tick(float deltaTime)
    {
        if (_currentClip == null || animationData == null) return;
        UpdateTimers(deltaTime * playbackSpeed);
        UpdateShaderProperties();
    }

    private void Initialize()
    {
        InitializeDependencies();
        bool isDataValid = ApplyAnimationDataToMaterial();
        if (isDataValid)
        {
            InitializeAndPlayDefault();
        }
    }

    private void InitializeDependencies()
    {
        if (_renderer == null) _renderer = GetComponent<Renderer>();
        if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();
    }

    private bool ApplyAnimationDataToMaterial()
    {
        if (animationData == null || !animationData.IsValid()) return false;

        // Cần đảm bảo MeshFilter cũng được gán mesh đã bake
        var meshFilter = GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            meshFilter.sharedMesh = animationData.bakedMesh;
        }

        ValidateArchetypeConstantsOnSharedMaterial();
        return true;
    }

    /// <summary>
    /// Đưa dữ liệu VAT BẤT BIẾN lên material dùng chung của archetype, không phải lên
    /// MaterialPropertyBlock của từng con.
    ///
    /// Đây là lỗi làm hỏng batching, và nó im lặng: texture vị trí, min và max là hằng số của
    /// archetype — mọi con DogPup dùng đúng một texture, đúng một cặp bounds — nhưng bản cũ ghi cả ba
    /// vào MPB RIÊNG của từng renderer. Một TEXTURE trong MPB thì không instance được, nên GPU
    /// instancing tự động bị vô hiệu hoàn toàn và mỗi enemy thành một draw riêng, dù material đã bật
    /// enableInstancing. Đo được: 26 con = +88 batch (~3.4/con).
    ///
    /// MPB từ đây chỉ còn giữ thứ THẬT SỰ khác nhau giữa các con: thời gian animation, trọng số
    /// crossfade, hit flash, dissolve.
    ///
    /// Ghi qua <c>sharedMaterial</c> chứ không phải <c>material</c>: chạm vào <c>material</c> sẽ tạo
    /// một bản sao runtime cho từng renderer, đúng thứ vừa mới gỡ bỏ. Chỉ ghi khi giá trị KHÁC, nên
    /// đây là thao tác một lần cho mỗi archetype chứ không phải mỗi frame.
    /// </summary>
    private void ValidateArchetypeConstantsOnSharedMaterial()
    {
        var material = _renderer != null ? _renderer.sharedMaterial : null;
        if (material == null) return;

        // KIỂM TRA, KHÔNG GHI.
        //
        // Bản trước tự ghi giá trị lên sharedMaterial lúc chạy. Nó che mất một asset thiếu dữ liệu:
        // material đi vào bản build không có position texture, và chỉ "đúng" sau khi một
        // MonoBehaviour kịp chạy — nghĩa là frame đầu vẫn sai, và ngoài Editor thì không ai vá hộ.
        // Hằng số của archetype giờ do VatMaterialAuthoring ghi sẵn vào asset lúc bake.
        if (material.HasProperty(PositionTexID) &&
            material.GetTexture(PositionTexID) != animationData.positionTexture)
        {
            Debug.LogError(
                $"[VAT] '{name}': material '{material.name}' mang _PositionTexture " +
                $"'{(material.GetTexture(PositionTexID) != null ? material.GetTexture(PositionTexID).name : "NULL")}' " +
                $"nhưng VAT_AnimationData '{animationData.name}' cần " +
                $"'{(animationData.positionTexture != null ? animationData.positionTexture.name : "NULL")}'. " +
                "Chạy ZombieWar/VAT/Author VAT constants onto materials.", this);
            return;
        }

        if (material.HasProperty(PositionMinID) &&
            (Vector3)material.GetVector(PositionMinID) != animationData.positionMinBounds)
        {
            Debug.LogError(
                $"[VAT] '{name}': material '{material.name}' có _PositionMin sai " +
                $"({(Vector3)material.GetVector(PositionMinID)} thay vì {animationData.positionMinBounds}). " +
                "Chạy ZombieWar/VAT/Author VAT constants onto materials.", this);
            return;
        }

        if (material.HasProperty(PositionMaxID) &&
            (Vector3)material.GetVector(PositionMaxID) != animationData.positionMaxBounds)
        {
            Debug.LogError(
                $"[VAT] '{name}': material '{material.name}' có _PositionMax sai " +
                $"({(Vector3)material.GetVector(PositionMaxID)} thay vì {animationData.positionMaxBounds}). " +
                "Chạy ZombieWar/VAT/Author VAT constants onto materials.", this);
        }
    }

    private void InitializeAndPlayDefault()
    {
        if (animationData != null && animationData.IsValid() && animationData.animationClips.Count > 0)
        {
            int clipIndex = Mathf.Clamp(defaultClipIndex, 0, animationData.animationClips.Count - 1);
            Play(animationData.animationClips[clipIndex].name);
        }
    }

    public void Play(string clipName) => Play(clipName, 0f);

    /// <summary>
    /// Starts a clip at <paramref name="normalizedStartPhase"/> of its length instead of frame zero.
    ///
    /// Exists for LOOPING LOCOMOTION only. A wave batch-spawns dozens of enemies within a frame or
    /// two, and every one of them entered idle/move at phase zero, so the whole crowd bobbed in
    /// lockstep - which reads as synchronized hopping even when their positions are calm. A stable
    /// per-instance phase breaks that up without touching baked data or playback speed.
    ///
    /// Never pass a non-zero phase for gameplay-timed one-shots (attack, hit, death, pounce,
    /// charge, burrow): their damage and telegraph windows are measured from frame zero.
    /// </summary>
    public void Play(string clipName, float normalizedStartPhase)
    {
        if (animationData == null || !animationData.TryGetClipInfo(clipName, out var newClip)) return;

        _currentClip = newClip;
        _currentTimeSeconds = StartTimeFor(newClip, normalizedStartPhase);
        _isBlending = false;
        _crossFadeTimer = 0;
        _previousClip = null;
    }

    public void CrossFade(string clipName, float duration) => CrossFade(clipName, duration, 0f);

    /// <inheritdoc cref="Play(string,float)"/>
    public void CrossFade(string clipName, float duration, float normalizedStartPhase)
    {
        if (animationData == null || !animationData.TryGetClipInfo(clipName, out var newClip)) return;
        if (_currentClip != null && _currentClip.name == newClip.name) return;

        _previousClip = _currentClip;
        _previousTimeSeconds = _currentTimeSeconds;
        _currentClip = newClip;
        _currentTimeSeconds = StartTimeFor(newClip, normalizedStartPhase);
        _crossFadeDuration = Mathf.Max(0, duration);
        _crossFadeTimer = 0;
        _isBlending = duration > 0.001f && _previousClip != null;
    }

    /// <summary>Phase is only meaningful on a looping clip; a one-shot always starts at zero even
    /// if a caller asks otherwise, so no gameplay timing can be skipped by accident.</summary>
    private static float StartTimeFor(VAT_AnimationData.ClipInfo clip, float normalizedStartPhase)
    {
        if (clip.wrapMode != WrapMode.Loop || clip.duration <= 0f || normalizedStartPhase <= 0f) return 0f;
        return Mathf.Repeat(normalizedStartPhase, 1f) * clip.duration;
    }

    /// <summary>Normalized position inside the current clip, for tests and diagnostics.</summary>
    public float CurrentNormalizedPhase =>
        _currentClip != null && _currentClip.duration > 0f
            ? Mathf.Repeat(_currentTimeSeconds / _currentClip.duration, 1f)
            : 0f;

    /// <summary>Name of the clip currently playing, or empty.</summary>
    public string CurrentClipName => _currentClip != null ? _currentClip.name : "";

    private void UpdateTimers(float adjustedDeltaTime)
    {
        _currentTimeSeconds += adjustedDeltaTime;
        if (_currentClip.wrapMode == WrapMode.Loop && _currentClip.duration > 0)
        {
            _currentTimeSeconds %= _currentClip.duration;
        }

        if (_isBlending)
        {
            _crossFadeTimer += adjustedDeltaTime;
            if (_crossFadeTimer >= _crossFadeDuration)
            {
                _isBlending = false;
                _previousClip = null;
            }

            if (_previousClip != null)
            {
                // Thời gian của clip cũ cũng phải được cập nhật nếu nó lặp lại
                _previousTimeSeconds += adjustedDeltaTime;
                if (_previousClip.wrapMode == WrapMode.Loop && _previousClip.duration > 0)
                {
                    _previousTimeSeconds %= _previousClip.duration;
                }
            }
        }
    }

    private void UpdateShaderProperties()
    {
        _renderer.GetPropertyBlock(_propertyBlock);

        float normalizedCurrentV = CalculateNormalizedVCoordinate(_currentClip, _currentTimeSeconds);
        _propertyBlock.SetFloat(CurrentTimeID, normalizedCurrentV);

        float blendWeight = 0f;
        if (_isBlending && _previousClip != null)
        {
            float normalizedPreviousV = CalculateNormalizedVCoordinate(_previousClip, _previousTimeSeconds);
            _propertyBlock.SetFloat(PreviousTimeID, normalizedPreviousV);
            blendWeight = _crossFadeDuration > 0 ? Mathf.Clamp01(_crossFadeTimer / _crossFadeDuration) : 1f;
        }
        _propertyBlock.SetFloat(BlendWeightID, blendWeight);

        _renderer.SetPropertyBlock(_propertyBlock);
    }

    // Tính toán tọa độ V chuẩn hóa (0-1) cho shader
    private float CalculateNormalizedVCoordinate(VAT_AnimationData.ClipInfo clip, float timeSeconds)
    {
        if (clip == null || animationData.positionTexture.height <= 1) return 0f;

        float progress = 0;
        // Xử lý các WrapMode khác nhau
        if (clip.duration > 0)
        {
            switch (clip.wrapMode)
            {
                case WrapMode.Loop:
                    progress = Mathf.Repeat(timeSeconds, clip.duration) / clip.duration;
                    break;
                case WrapMode.PingPong:
                    progress = Mathf.PingPong(timeSeconds, clip.duration) / clip.duration;
                    break;
                default: // Once, ClampForever
                    progress = Mathf.Clamp01(timeSeconds / clip.duration);
                    break;
            }
        }

        // (frameCount - 1) là số khoảng thời gian giữa các frame
        float frameIndexInClip = progress * (clip.frameCount - 1);
        float absoluteFrame = clip.startFrame + frameIndexInClip;

        // Thêm 0.5 để sample vào giữa texel theo chiều dọc, tận dụng bilinear filtering
        return (absoluteFrame + 0.5f) / animationData.positionTexture.height;
    }

#if UNITY_EDITOR
    private void EditorTick()
    {
        // Chỉ update nếu không ở Play Mode và object đang được hiển thị
        if (Application.isPlaying || _renderer == null || !_renderer.isVisible || animationData == null) return;

        double currentTime = EditorApplication.timeSinceStartup;
        float deltaTime = (float)(currentTime - _lastEditorUpdateTime);
        _lastEditorUpdateTime = currentTime;

        Tick(deltaTime);
        SceneView.RepaintAll();
    }

    private void OnDrawGizmosSelected()
    {
        if (!showAnimationBounds || animationData == null || !animationData.IsValid()) return;

        Vector3 localCenter = (animationData.positionMinBounds + animationData.positionMaxBounds) * 0.5f;
        Vector3 localSize = animationData.positionMaxBounds - animationData.positionMinBounds;

        Gizmos.color = new Color(0.1f, 0.9f, 0.5f, 0.35f);
        // Gizmo phải được vẽ trong không gian world của object
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(localCenter, localSize);

        Gizmos.color = new Color(0.1f, 0.9f, 0.5f, 0.9f);
        Gizmos.DrawWireCube(localCenter, localSize);
    }
#endif
}