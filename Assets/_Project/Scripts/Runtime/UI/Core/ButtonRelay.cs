using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>Forward click của button này sang button khác (dock PLAY tròn → CTA PLAY chính).</summary>
    [RequireComponent(typeof(Button))]
    public sealed class ButtonRelay : MonoBehaviour
    {
        [SerializeField] private Button target;

        public Button Target { get => target; set => target = value; }

        void Awake()
        {
            GetComponent<Button>().onClick.AddListener(() =>
            {
                if (target != null) target.onClick.Invoke();
            });
        }
    }
}
