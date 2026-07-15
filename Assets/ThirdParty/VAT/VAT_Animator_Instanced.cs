using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
public class VAT_Animator_Instanced : MonoBehaviour
{
    public VAT_AnimationData animationData;
    public float playbackSpeed = 1.0f;

    [Tooltip("Animation clip to play on Start.")]
    public int defaultClipIndex = 0;

    [Header("Visualization")]
    [Tooltip("If checked, draws a gizmo showing the total animation bounds.")]
    public bool showAnimationBounds = true;

    private Renderer _renderer;
    private MaterialPropertyBlock _propertyBlock;

    private static readonly int PositionTexID = Shader.PropertyToID("_PositionTexture");
    private static readonly int PositionMinID = Shader.PropertyToID("_PositionMin");
    private static readonly int PositionMaxID = Shader.PropertyToID("_PositionMax");
    private static readonly int TextureHeightID = Shader.PropertyToID("_TextureHeight");

    private static readonly int ClipStartFrameProp = Shader.PropertyToID("_ClipStartFrame");
    private static readonly int ClipFrameCountProp = Shader.PropertyToID("_ClipFrameCount");
    private static readonly int ClipDurationProp = Shader.PropertyToID("_ClipDuration");
    private static readonly int PlaybackSpeedProp = Shader.PropertyToID("_PlaybackSpeed");
    private static readonly int AnimationStartTimeProp = Shader.PropertyToID("_AnimationStartTime");
    private static readonly int WrapModeProp = Shader.PropertyToID("_WrapMode");

    private void OnEnable()
    {
        InitializeAndPlayDefault();
    }

    private void OnValidate()
    {
        if (this.isActiveAndEnabled)
        {
            InitializeAndPlayDefault();
        }
    }

    private void InitializeAndPlayDefault()
    {
        InitializeDependencies();
        bool isDataValid = ApplyStaticAnimationData();
        if (isDataValid)
        {
            if (animationData.animationClips.Count > 0)
            {
                int clipIndex = Mathf.Clamp(defaultClipIndex, 0, animationData.animationClips.Count - 1);
                Play(animationData.animationClips[clipIndex].name);
            }
        }
    }

    private void InitializeDependencies()
    {
        if (_renderer == null) _renderer = GetComponent<Renderer>();
        if (_propertyBlock == null) _propertyBlock = new MaterialPropertyBlock();
    }

    private bool ApplyStaticAnimationData()
    {
        if (animationData == null || !animationData.IsValid()) return false;

        var meshFilter = GetComponent<MeshFilter>();
        meshFilter.sharedMesh = animationData.bakedMesh;

        _renderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetTexture(PositionTexID, animationData.positionTexture);
        _propertyBlock.SetVector(PositionMinID, animationData.positionMinBounds);
        _propertyBlock.SetVector(PositionMaxID, animationData.positionMaxBounds);
        _propertyBlock.SetFloat(TextureHeightID, animationData.positionTexture.height);
        _renderer.SetPropertyBlock(_propertyBlock);

        return true;
    }

    public void Play(string clipName)
    {
        if (animationData == null || !animationData.TryGetClipInfo(clipName, out var newClip)) return;

        uint wrapMode;
        switch (newClip.wrapMode)
        {
            case WrapMode.Loop:
                wrapMode = 1u;
                break;
            case WrapMode.PingPong:
                wrapMode = 2u;
                break;
            default: // Once, ClampForever
                wrapMode = 0u;
                break;
        }

        _renderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetFloat(ClipStartFrameProp, newClip.startFrame);
        _propertyBlock.SetFloat(ClipFrameCountProp, newClip.frameCount);
        _propertyBlock.SetFloat(ClipDurationProp, newClip.duration);
        _propertyBlock.SetFloat(PlaybackSpeedProp, playbackSpeed);
        _propertyBlock.SetFloat(AnimationStartTimeProp, Time.time);
        _propertyBlock.SetFloat(WrapModeProp, wrapMode);
        _renderer.SetPropertyBlock(_propertyBlock);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!showAnimationBounds || animationData == null || !animationData.IsValid()) return;

        Vector3 localCenter = (animationData.positionMinBounds + animationData.positionMaxBounds) * 0.5f;
        Vector3 localSize = animationData.positionMaxBounds - animationData.positionMinBounds;

        Gizmos.color = new Color(0.1f, 0.9f, 0.5f, 0.35f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(localCenter, localSize);

        Gizmos.color = new Color(0.1f, 0.9f, 0.5f, 0.9f);
        Gizmos.DrawWireCube(localCenter, localSize);
    }
#endif
}