using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// Outline width multiplier for one camera (2026-10-02). A camera that renders into a texture shown
    /// smaller on screen (the menu hero: a 1440 x 900 texture drawn about 1060 canvas units wide)
    /// would otherwise get a thinner line than the game camera. Both the texture's on-screen size and
    /// the game's line width follow the screen width, so one constant per camera keeps them equal.
    /// </summary>
    [DisallowMultipleComponent]
    public class OutlineCameraWidth : MonoBehaviour
    {
        public float multiplier = 1f;
    }
}
