using System.Collections;
using UnityEngine;

namespace ZombieWar.UI
{
    /// <summary>
    /// Base cho mọi màn UI (15 màn wireframe). Mỗi screen là 1 prefab con dưới UIRoot,
    /// bật/tắt bằng CanvasGroup (không SetActive canvas → tránh rebuild layout).
    /// Lifecycle: OnShow (mỗi lần vào) / OnFocus (quay lại top stack) / OnHide.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIScreen : MonoBehaviour
    {
        [Tooltip("Bấm Android Back / Escape có pop màn này không (HUD, FTUE thì không).")]
        [SerializeField] private bool allowEscapePop = true;

        public bool AllowEscapePop => allowEscapePop;
        public bool IsShown { get; private set; }

        /// <summary>Màn che kín (full-screen) → khi bị đè, màn dưới ẩn hẳn. Popup override = false để màn dưới hiện mờ.</summary>
        public virtual bool IsOpaque => true;

        private CanvasGroup _cg;
        private RectTransform _rt;
        private Coroutine _anim;

        protected virtual void Awake()
        {
            _cg = GetComponent<CanvasGroup>();
            _rt = (RectTransform)transform;
            _cg.alpha = 0f;
            SetInteractable(false);
        }

        // ------------------------------------------------ UIManager entry points
        public void Show()
        {
            if (IsShown) { Focus(); return; }
            IsShown = true;
            gameObject.SetActive(true);
            SetInteractable(true);
            OnShow();
            Restart(UITransition.Show(_cg, _rt));
        }

        public void Hide(bool instant = false)
        {
            if (!IsShown && !instant) return;
            IsShown = false;
            SetInteractable(false);
            OnHide();
            if (instant || !gameObject.activeInHierarchy)
            {
                if (_cg != null) _cg.alpha = 0f;
                gameObject.SetActive(false);
                return;
            }
            Restart(UITransition.Hide(_cg, () => gameObject.SetActive(false)));
        }

        /// <summary>Màn phía trên bị pop → màn này trở lại top stack.</summary>
        public void Focus()
        {
            SetInteractable(true);
            OnFocus();
        }

        /// <summary>Bị màn khác push đè lên (vẫn hiển thị mờ phía sau nếu là popup).</summary>
        public void Blur() => SetInteractable(false);

        // ------------------------------------------------ overrides
        protected virtual void OnShow() { }
        protected virtual void OnFocus() { }
        protected virtual void OnHide() { }

        /// <summary>Escape/back khi đang top. Trả true nếu đã tự xử lý (không pop).</summary>
        public virtual bool OnEscape() => false;

        // ------------------------------------------------ internals
        private void SetInteractable(bool on)
        {
            if (_cg == null) _cg = GetComponent<CanvasGroup>();
            _cg.interactable = on;
            _cg.blocksRaycasts = on;
        }

        private void Restart(IEnumerator routine)
        {
            if (_anim != null) StopCoroutine(_anim);
            _anim = StartCoroutine(routine);
        }
    }
}
