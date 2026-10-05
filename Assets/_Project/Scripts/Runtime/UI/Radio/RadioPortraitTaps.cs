using UnityEngine;
using UnityEngine.EventSystems;

namespace ZombieWar.UI
{
    /// <summary>
    /// Counts taps on a radio card's portrait across the session; the agents react at 5 and 10
    /// (lines vo_jiho_egg_tap5 and vo_mai_egg_tap10, recorded 03/10, wired 05/10).
    /// </summary>
    public sealed class RadioPortraitTaps : MonoBehaviour, IPointerClickHandler
    {
        static int _taps;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _taps = 0;

        public void OnPointerClick(PointerEventData eventData) => ZombieWar.Audio.RadioDirector.PortraitTapped(++_taps);
    }
}
