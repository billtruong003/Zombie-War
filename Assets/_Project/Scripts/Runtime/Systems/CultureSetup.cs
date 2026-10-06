using System.Globalization;
using UnityEngine;

namespace ZombieWar
{
    /// <summary>
    /// Numbers read the same on every phone (07/10). With the device's culture, "N0" printed
    /// "1.886" on a Vietnamese or Indonesian phone and "1,886" elsewhere, next to compact amounts
    /// like "96.7K" that always use a dot. Text is English until the localization pass, which will
    /// choose its own number format per language on purpose.
    /// </summary>
    static class CultureSetup
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void UseInvariant()
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        }
    }
}
