using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer))]
public class VAT_Animator : MonoBehaviour
{
    public VAT_AnimationData animationData;
    public float playbackSpeed = 1.0f;
    public int defaultClipIndex = 0;
    public bool showAnimationBounds = true;

    private Renderer _renderer;
    private MaterialPropertyBlock _pb;
    private VAT_AnimationData.ClipInfo _curClip, _prevClip;
    private float _curTime, _prevTime, _fadeTimer, _fadeDur;
    private bool _isBlending;

    private static readonly int CurTimeID = Shader.PropertyToID("_CurrentAnimNormalizedTime");
    private static readonly int PrevTimeID = Shader.PropertyToID("_PreviousAnimNormalizedTime");
    private static readonly int BlendID = Shader.PropertyToID("_AnimationBlendWeight");
    private static readonly int PosTexID = Shader.PropertyToID("_PositionTexture");
    private static readonly int NormTexID = Shader.PropertyToID("_NormalTexture");
    private static readonly int PosMinID = Shader.PropertyToID("_PositionMin");
    private static readonly int PosMaxID = Shader.PropertyToID("_PositionMax");

#if UNITY_EDITOR
    private double _lastEditTime;
#endif

    private void OnEnable()
    {
        Init();
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

    private void Update() { if (Application.isPlaying) Tick(Time.deltaTime); }
    private void OnValidate() { if (isActiveAndEnabled) Init(); }

    private void Tick(float dt)
    {
        if (_curClip == null || animationData == null) return;
        UpdateTimer(dt * playbackSpeed);
        UpdateShader();
    }

    private void Init()
    {
        if (_renderer == null) _renderer = GetComponent<Renderer>();
        if (_pb == null) _pb = new MaterialPropertyBlock();

        if (animationData != null && animationData.IsValid())
        {
            var mf = GetComponent<MeshFilter>();
            if (mf) mf.sharedMesh = animationData.bakedMesh;

            _renderer.GetPropertyBlock(_pb);
            _pb.SetTexture(PosTexID, animationData.positionTexture);
            _pb.SetTexture(NormTexID, animationData.normalTexture);
            _pb.SetVector(PosMinID, animationData.positionMinBounds);
            _pb.SetVector(PosMaxID, animationData.positionMaxBounds);
            _renderer.SetPropertyBlock(_pb);

            if (_curClip == null && animationData.animationClips.Count > 0)
                Play(animationData.animationClips[Mathf.Clamp(defaultClipIndex, 0, animationData.animationClips.Count - 1)].name);
        }
    }

    public void Play(string name)
    {
        if (animationData == null || !animationData.TryGetClipInfo(name, out var clip)) return;
        _curClip = clip;
        _curTime = 0;
        _isBlending = false;
        _prevClip = null;
    }

    public void CrossFade(string name, float dur)
    {
        if (animationData == null || !animationData.TryGetClipInfo(name, out var clip)) return;
        if (_curClip != null && _curClip.name == clip.name) return;
        _prevClip = _curClip;
        _prevTime = _curTime;
        _curClip = clip;
        _curTime = 0;
        _fadeDur = Mathf.Max(0, dur);
        _fadeTimer = 0;
        _isBlending = dur > 0.001f && _prevClip != null;
    }

    private void UpdateTimer(float dt)
    {
        _curTime += dt;
        if (_curClip.duration > 0 && _curClip.wrapMode == WrapMode.Loop) _curTime %= _curClip.duration;

        if (_isBlending)
        {
            _fadeTimer += dt;
            if (_fadeTimer >= _fadeDur) { _isBlending = false; _prevClip = null; }
            if (_prevClip != null)
            {
                _prevTime += dt;
                if (_prevClip.duration > 0 && _prevClip.wrapMode == WrapMode.Loop) _prevTime %= _prevClip.duration;
            }
        }
    }

    private void UpdateShader()
    {
        _renderer.GetPropertyBlock(_pb);
        _pb.SetFloat(CurTimeID, GetNormTime(_curClip, _curTime));
        float w = 0f;
        if (_isBlending && _prevClip != null)
        {
            _pb.SetFloat(PrevTimeID, GetNormTime(_prevClip, _prevTime));
            w = _fadeDur > 0 ? Mathf.Clamp01(_fadeTimer / _fadeDur) : 1f;
        }
        _pb.SetFloat(BlendID, w);
        _renderer.SetPropertyBlock(_pb);
    }

    private float GetNormTime(VAT_AnimationData.ClipInfo clip, float t)
    {
        if (clip == null || animationData.positionTexture.height <= 1) return 0;
        float p = 0;
        if (clip.duration > 0)
        {
            p = clip.wrapMode switch
            {
                WrapMode.Loop => Mathf.Repeat(t, clip.duration) / clip.duration,
                WrapMode.PingPong => Mathf.PingPong(t, clip.duration) / clip.duration,
                _ => Mathf.Clamp01(t / clip.duration)
            };
        }
        float absFrame = clip.startFrame + p * (clip.frameCount - 1);
        return (absFrame + 0.5f) / animationData.positionTexture.height;
    }

#if UNITY_EDITOR
    private void EditorTick()
    {
        if (Application.isPlaying || _renderer == null || !_renderer.isVisible || animationData == null) return;
        double t = EditorApplication.timeSinceStartup;
        Tick((float)(t - _lastEditTime));
        _lastEditTime = t;
        SceneView.RepaintAll();
    }

    private void OnDrawGizmosSelected()
    {
        if (!showAnimationBounds || animationData == null || !animationData.IsValid()) return;
        Vector3 c = (animationData.positionMinBounds + animationData.positionMaxBounds) * 0.5f;
        Vector3 s = animationData.positionMaxBounds - animationData.positionMinBounds;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0, 1, 0.5f, 0.3f);
        Gizmos.DrawCube(c, s);
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(c, s);
    }
#endif
}