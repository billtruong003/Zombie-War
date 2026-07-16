using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "VAT_AnimationData", menuName = "BillTheDev/VAT/Animation Data")]
public class VAT_AnimationData : ScriptableObject
{
    [System.Serializable]
    public class ClipInfo
    {
        public string name;
        public int startFrame;
        public int frameCount;
        public float duration;
        public WrapMode wrapMode;
    }

    public Mesh bakedMesh;
    public Texture2D positionTexture;
    public Vector3 positionMinBounds;
    public Vector3 positionMaxBounds;
    public List<ClipInfo> animationClips = new List<ClipInfo>();

    private Dictionary<string, ClipInfo> _clipLookup;

    private void OnEnable() => InitializeLookup();

    public bool TryGetClipInfo(string clipName, out ClipInfo clipInfo)
    {
        if (_clipLookup == null || _clipLookup.Count != animationClips.Count)
        {
            InitializeLookup();
        }
        return _clipLookup.TryGetValue(clipName, out clipInfo);
    }

    public bool IsValid()
    {
        return bakedMesh != null && positionTexture != null && animationClips.Count > 0;
    }

    private void InitializeLookup()
    {
        _clipLookup = animationClips.ToDictionary(c => c.name);
    }
}