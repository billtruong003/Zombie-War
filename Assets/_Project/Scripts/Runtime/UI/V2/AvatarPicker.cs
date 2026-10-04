using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// Profile picture sheet (owner 2026-09-27): AVATAR and FRAME tabs over one grid. The preview
    /// shows the picked avatar in the picked frame; locked items say how to win them. Equip saves
    /// the pair to the profile.
    /// </summary>
    public sealed class AvatarPicker : MonoBehaviour
    {
        [Serializable]
        public sealed class Cell { public Button button; public AvatarView view; public GameObject locked; public TMP_Text label; public GameObject selected; }

        [SerializeField] private GameObject root;
        [Tooltip("G12.8: the sheet that pops in.")]
        [SerializeField] private RectTransform sheet;
        [SerializeField] private Button closeButton;
        [SerializeField] private AvatarView preview;
        [SerializeField] private TMP_Text previewName;
        [SerializeField] private TMP_Text previewUnlock;
        [SerializeField] private Button avatarTab;
        [SerializeField] private Button frameTab;
        [SerializeField] private Cell[] cells = new Cell[12];
        [SerializeField] private Button equipButton;
        [SerializeField] private TMP_Text equipLabel;

        bool _frames;
        AvatarCatalog.Avatar _avatar;
        AvatarCatalog.Frame _frame;

        void Awake()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (avatarTab != null) avatarTab.onClick.AddListener(() => { UIFeedback.Tap(); _frames = false; Refresh(); });
            if (frameTab != null) frameTab.onClick.AddListener(() => { UIFeedback.Tap(); _frames = true; Refresh(); });
            for (int i = 0; i < cells.Length; i++) { int idx = i; if (cells[i]?.button != null) cells[i].button.onClick.AddListener(() => Pick(idx)); }
            if (equipButton != null) equipButton.onClick.AddListener(Equip);
            // No SetActive(false) here: Awake runs on the first Open, which would close it at once.
        }

        public bool IsOpen => root != null && root.activeSelf;

        public void Open(bool frames)
        {
            _frames = frames;
            _avatar = AvatarCatalog.CurrentAvatar;
            _frame = AvatarCatalog.CurrentFrame;
            if (root == null) return;
            root.SetActive(true);
            root.transform.SetAsLastSibling();
            if (sheet != null) UIFx.PopIn(sheet, 0f, 0.92f, 0.22f);
            UIFeedback.Tap();
            Refresh();
        }

        public void Close() { UIFeedback.Back(); if (root != null) root.SetActive(false); }

        void Pick(int i)
        {
            UIFeedback.Tap();
            if (_frames) { if (i < AvatarCatalog.Frames.Length) _frame = AvatarCatalog.Frames[i]; }
            else if (i < AvatarCatalog.Avatars.Length) _avatar = AvatarCatalog.Avatars[i];
            Refresh();
        }

        void Equip()
        {
            bool avatarOk = _avatar != null && _avatar.unlocked(), frameOk = _frame != null && _frame.unlocked();
            if (!(_frames ? frameOk : avatarOk)) { UIFeedback.Error(); Toast.Show(_frames ? _frame.unlockText : _avatar.unlockText); return; }
            if (avatarOk) PlayerProfile.SetAvatar(_avatar.id);
            if (frameOk) PlayerProfile.SetFrame(_frame.id);
            UIFeedback.Equip();
            Refresh();
        }

        void Refresh()
        {
            if (preview != null) preview.Show(_avatar, _frame);
            bool unlocked = _frames ? _frame.unlocked() : _avatar.unlocked();
            if (previewName != null) previewName.text = (_frames ? _frame.name : _avatar.name).ToUpperInvariant();
            if (previewUnlock != null) previewUnlock.text = unlocked ? (_frames ? "FRAME" : "AVATAR") + " · UNLOCKED" : "LOCKED · " + (_frames ? _frame.unlockText : _avatar.unlockText);
            bool wearing = _frames ? _frame.id == AvatarCatalog.CurrentFrame.id : _avatar.id == AvatarCatalog.CurrentAvatar.id;
            if (equipLabel != null) equipLabel.text = !unlocked ? "LOCKED" : wearing ? "EQUIPPED" : "EQUIP";
            if (equipButton != null)
            {
                equipButton.interactable = unlocked && !wearing;
                if (!equipButton.TryGetComponent(out CanvasGroup cg)) cg = equipButton.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = unlocked && !wearing ? 1f : 0.5f;
            }

            SetTab(avatarTab, !_frames); SetTab(frameTab, _frames);
            int count = _frames ? AvatarCatalog.Frames.Length : AvatarCatalog.Avatars.Length;
            for (int i = 0; i < cells.Length; i++)
            {
                var c = cells[i]; if (c?.button == null) continue;
                bool has = i < count; c.button.gameObject.SetActive(has);
                if (!has) continue;
                bool open, on;
                if (_frames)
                {
                    var f = AvatarCatalog.Frames[i];
                    c.view.Show(_avatar, f); open = f.unlocked(); on = f == _frame;
                    if (c.label != null) c.label.text = f.name;
                }
                else
                {
                    var a = AvatarCatalog.Avatars[i];
                    c.view.Show(a, AvatarCatalog.Frames[0]); open = a.unlocked(); on = a == _avatar;
                    if (c.label != null) c.label.text = a.name;
                }
                if (c.locked != null) c.locked.SetActive(!open);
                if (c.selected != null) c.selected.SetActive(on);
            }
        }

        static void SetTab(Button b, bool on)
        {
            if (b == null) return;
            ThemeTint.Set(b.targetGraphic, on ? ThemeRole.Ink : ThemeRole.Card);
            ThemeTint.Set(b.GetComponentInChildren<TMP_Text>(true), on ? ThemeRole.OnInk : ThemeRole.Dim);
        }
    
#if UNITY_EDITOR
        /// G12.8: wires the sheet the picker used to find by path; returns the paths not found.
        public System.Collections.Generic.List<string> EditorWire()
        {
            var m = new System.Collections.Generic.List<string>();
            sheet = WireUtil.Find<RectTransform>(root != null ? root.transform : null, "Sheet", m);
            return m;
        }

        public System.Collections.Generic.List<string> EditorUnwired() => WireUtil.Nulls(("sheet", sheet));
#endif
}
}
