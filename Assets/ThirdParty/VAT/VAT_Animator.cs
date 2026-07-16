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

        _renderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetTexture(PositionTexID, animationData.positionTexture);
        _propertyBlock.SetVector(PositionMinID, animationData.positionMinBounds);
        _propertyBlock.SetVector(PositionMaxID, animationData.positionMaxBounds);
        _renderer.SetPropertyBlock(_propertyBlock);
        return true;
    }

    private void InitializeAndPlayDefault()
    {
        if (animationData != null && animationData.IsValid() && animationData.animationClips.Count > 0)
        {
            int clipIndex = Mathf.Clamp(defaultClipIndex, 0, animationData.animationClips.Count - 1);
            Play(animationData.animationClips[clipIndex].name);
        }
    }

    public void Play(string clipName)
    {
        if (animationData == null || !animationData.TryGetClipInfo(clipName, out var newClip)) return;

        _currentClip = newClip;
        _currentTimeSeconds = 0;
        _isBlending = false;
        _crossFadeTimer = 0;
        _previousClip = null;
    }

    public void CrossFade(string clipName, float duration)
    {
        if (animationData == null || !animationData.TryGetClipInfo(clipName, out var newClip)) return;
        if (_currentClip != null && _currentClip.name == newClip.name) return;

        _previousClip = _currentClip;
        _previousTimeSeconds = _currentTimeSeconds;
        _currentClip = newClip;
        _currentTimeSeconds = 0;
        _crossFadeDuration = Mathf.Max(0, duration);
        _crossFadeTimer = 0;
        _isBlending = duration > 0.001f && _previousClip != null;
    }

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