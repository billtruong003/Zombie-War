using UnityEngine;

namespace ZombieWar.Skills.Powers
{
    /// <summary>Tumbles a pooled effect's mesh (the Meteor's rock) while it flies. No allocation.</summary>
    public sealed class SpinWhileAlive : MonoBehaviour
    {
        [SerializeField] private Vector3 degreesPerSecond = new(170f, 90f, 60f);

        private void Update() => transform.Rotate(degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
