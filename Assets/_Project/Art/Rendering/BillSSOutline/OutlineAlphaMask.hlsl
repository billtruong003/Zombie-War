#ifndef ZW_OUTLINE_ALPHA_MASK_INCLUDED
#define ZW_OUTLINE_ALPHA_MASK_INCLUDED

// Outline mask carried in the colour buffer's alpha (2026-10-03). The outline used to redraw every
// outlined object a second time into its own mask texture; with 100 enemies that second draw was
// half of all the enemy triangles of the frame. Now the shaders of the outlined crowd (VAT enemies,
// environment pieces) write alpha 0 in their normal colour pass when their renderer is on one of
// these rendering layers, and everything else writes 1. The outline composite reads 1 - alpha as
// the mask, before the transparents can blend into it.
//
// Set per camera by OutlineFeature: 0 when the path is off (scene view, captures into textures,
// a colour format without alpha), so those keep their ordinary alpha.
float _ZWOutlineAlphaLayers;

half ZWOutlineAlpha(half alpha)
{
    uint layers = (uint)_ZWOutlineAlphaLayers;
    if (layers == 0u) return alpha;
    return (GetMeshRenderingLayer() & layers) != 0u ? 0.0h : 1.0h;
}

#endif
