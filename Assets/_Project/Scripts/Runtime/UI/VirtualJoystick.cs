using BillGameCore;

namespace ZombieWar
{
    /// <summary>
    /// Shell tương thích: toàn bộ hành vi nằm ở BillGameCore.BillVirtualJoystick (floating origin,
    /// pointer-id lock, dead zone). Giữ class + namespace + tên field serialized (background/handle)
    /// để prefab HUD và mọi reference PlayerMovement/HUD contract cũ không vỡ.
    /// </summary>
    public class VirtualJoystick : BillVirtualJoystick
    {
    }
}
