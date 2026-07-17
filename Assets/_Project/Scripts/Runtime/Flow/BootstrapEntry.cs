using BillGameCore;
using UnityEngine;

namespace ZombieWar
{
    /// Lives on the persistent Bootstrap scene. Waits for Bill services to finish their
    /// [RuntimeInitializeOnLoadMethod] boot (order between that and MonoBehaviour Start is not
    /// guaranteed, so we poll IsReady instead of subscribing) and then drives the app into the menu.
    public class BootstrapEntry : MonoBehaviour
    {
        private bool _entered;

        private void Update()
        {
            if (_entered || !Bill.IsReady) return;
            _entered = true;
            GameFlow.EnterMenu();
        }
    }
}
