// Imported from https://github.com/billtruong003/Bill-SSOutline @ 2acf5b72 (pinned)
// Isolated under ZombieWar.Rendering.BillSSOutline for A/B evaluation against the STW outline.
// Do not merge into the Stylized Toon World Kit.
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ZombieWar.Rendering.BillSSOutline
{
    [Serializable, VolumeComponentMenu("Post-processing/Custom/Outline")]
    public class OutlineVolume : VolumeComponent, IPostProcessComponent
    {
        public enum OutlineMode { FullScreen, SelectionOnly, Mixed }
        public enum OutlineAlgorithm { RobertsCross, Sobel }
        public enum DebugMode { None, Depth, Normals, Color, EdgeOnly, MaskOnly, Occlusion }

        public BoolParameter isActive = new BoolParameter(false);
        public EnumParameter<OutlineMode> mode = new EnumParameter<OutlineMode>(OutlineMode.FullScreen);

        [Header("Masking")]
        // Optimization over stock (2acf5b7): default Nothing, not Everything - stock's -1 made
        // every camera redraw the whole scene into the selection mask by default.
        public LayerMaskParameter selectionLayer = new LayerMaskParameter(0);
        public LayerMaskParameter occlusionLayer = new LayerMaskParameter(0); // Objects that hide outline

        [Header("Settings")]
        public EnumParameter<DebugMode> debugMode = new EnumParameter<DebugMode>(DebugMode.None);
        public EnumParameter<OutlineAlgorithm> algorithm = new EnumParameter<OutlineAlgorithm>(OutlineAlgorithm.Sobel);

        public ClampedIntParameter thickness = new ClampedIntParameter(2, 1, 10);
        public ColorParameter outlineColor = new ColorParameter(new Color(0, 1, 0, 1), true, false, true);

        public ClampedFloatParameter depthThreshold = new ClampedFloatParameter(1.5f, 0f, 10f);
        public ClampedFloatParameter normalThreshold = new ClampedFloatParameter(0.4f, 0f, 1f);
        public ClampedFloatParameter colorThreshold = new ClampedFloatParameter(0.2f, 0f, 1f);

        public BoolParameter useDepth = new BoolParameter(true);
        public BoolParameter useNormals = new BoolParameter(true);
        public BoolParameter useColor = new BoolParameter(false);

        public BoolParameter useDistanceFade = new BoolParameter(false);
        public FloatParameter fadeDistanceStart = new FloatParameter(0f);
        public FloatParameter fadeDistanceEnd = new FloatParameter(50f);

        public BoolParameter useHeightFade = new BoolParameter(false);
        public FloatParameter fadeHeightMin = new FloatParameter(0f);
        public FloatParameter fadeHeightMax = new FloatParameter(10f);

        public bool IsActive() => isActive.value;
        public bool IsTileCompatible() => false;
    }
}
