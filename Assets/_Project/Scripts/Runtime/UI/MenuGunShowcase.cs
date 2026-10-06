using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ZombieWar.UI
{
    /// <summary>
    /// Home lobby life (owner 2026-09-29): every so often the menu character turns a little, raises
    /// the equipped gun and fires a short burst that kicks, shakes and flashes, then lowers it, so
    /// the player sees their character with their gun. Only while Home is the top screen.
    ///
    /// Runs on the preview stage at runtime (added by <see cref="MenuCharacterStage"/>), so the Menu
    /// scene is not edited. The aim poses come from Resources/Menu/MenuShowcase.controller (Idle,
    /// Rifle, Pistol, IK pass on; built by "HordeCall/Menu/Build Showcase Controller"). The gun is
    /// held in front of the right shoulder, sized to the character's arm, and <see cref="MenuGunIK"/>
    /// puts the hands on its authored grips.
    /// </summary>
    public sealed class MenuGunShowcase : MonoBehaviour
    {
        const string ControllerPath = "Menu/MenuShowcase";
        const float TurnYaw = -62f;   // aim across the screen so the gun reads side-on, gun hand toward the camera

        public Vector2 interval = new(10f, 16f);
        public Vector2 shotsPerBurst = new(5, 9);

        Animator _animator;
        MenuGunIK _ik;
        Transform _pivot, _shoulderR, _shoulderL;
        HomeScreen _home;
        bool _homeSearched;
        CanvasGroup _homeGroup;
        GameObject _gun;
        WeaponData _gunData;
        Transform _gripR, _gripL;
        ParticleSystem _flash;
        Quaternion _pivotRest;
        float _next, _yaw, _kick, _raise, _arm;
        bool _busy, _hold;

        // Home shows the character through a wide texture so a raised gun is never cut at the
        // sides (owner 2026-09-29). The stage texture stays as it is for Studio, Profile and the
        // outfit banner; the camera only renders wide while Home is on top.
        const float WideAspect = 1.6f;
        Camera _cam;
        RenderTexture _baseRT, _wideRT;
        RawImage _homeImage;
        bool _wide;

        public void Init(Animator animator, Transform characterRoot, Camera cam = null)
        {
            _cam = cam;
            _baseRT = cam != null ? cam.targetTexture : null;
            _animator = animator;
            _pivot = characterRoot != null ? characterRoot.parent : null;
            if (_pivot != null) _pivotRest = _pivot.localRotation;
            var controller = Resources.Load<RuntimeAnimatorController>(ControllerPath);
            if (_animator == null || controller == null || !_animator.isHuman) return;
            _animator.runtimeAnimatorController = controller;
            // TryGetComponent, not GetComponent() ?? Add(): in the Editor a missing component is a
            // "fake null" that ?? treats as real, so the IK was never added in Play Mode.
            if (!_animator.TryGetComponent(out _ik)) _ik = _animator.gameObject.AddComponent<MenuGunIK>();
            _shoulderR = _animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            _shoulderL = _animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var lower = _animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var hand = _animator.GetBoneTransform(HumanBodyBones.RightHand);
            _arm = Vector3.Distance(_shoulderR.position, lower.position) + Vector3.Distance(lower.position, hand.position);
            _next = Time.unscaledTime + 4f;
        }

        bool HomeOnTop()
        {
            if (_home == null && !_homeSearched)
            {
                _homeSearched = true;   // one scene search, not one per frame (Update calls this twice)
                _home = FindFirstObjectByType<HomeScreen>(FindObjectsInactive.Include);
                _homeGroup = _home != null ? _home.GetComponent<CanvasGroup>() : null;
            }
            return _home != null && _home.IsShown && _homeGroup != null && _homeGroup.interactable;
        }

        // The camera renders only while a screen shows its picture (Home, Studio, Profile, the outfit
        // banner): it drew the character and its outline every frame under the Shop, Arsenal and
        // Gacha too (07/10 audit).
        readonly System.Collections.Generic.List<RawImage> _viewers = new();
        float _rescanAt;

        void UpdateCameraOn()
        {
            if (_cam == null || _baseRT == null) return;
            if (Time.unscaledTime >= _rescanAt)
            {
                _rescanAt = Time.unscaledTime + 5f;   // screens added after the menu loaded
                _viewers.Clear();
                foreach (var ri in FindObjectsByType<RawImage>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (ri.texture == _baseRT || (_wideRT != null && ri.texture == _wideRT)) _viewers.Add(ri);
            }
            bool seen = _viewers.Count == 0;   // nothing found: keep rendering rather than go blank
            for (int i = 0; i < _viewers.Count && !seen; i++)
                seen = _viewers[i] != null && _viewers[i].isActiveAndEnabled;
            if (_cam.enabled != seen) _cam.enabled = seen;
        }

        void Update()
        {
            UpdateCameraOn();
            SetWide(HomeOnTop());
            if (_busy || _ik == null) return;
            if (!HomeOnTop()) { _next = Mathf.Max(_next, Time.unscaledTime + 3f); return; }
            if (Time.unscaledTime < _next) return;
            StartCoroutine(Show());
        }

        void SetWide(bool on)
        {
            if (on == _wide || _cam == null || _baseRT == null) return;
            if (on && _homeImage == null && !FindHomeImage()) return;
            _wide = on;
            if (on && _wideRT == null)
                _wideRT = new RenderTexture(Mathf.RoundToInt(_baseRT.height * WideAspect), _baseRT.height, 24, _baseRT.format) { antiAliasing = Mathf.Max(1, _baseRT.antiAliasing), name = "MenuCharacterWide" };
            _cam.targetTexture = on ? _wideRT : _baseRT;
            _cam.ResetAspect();
            if (on) _homeImage.texture = _wideRT;
        }

        /// Home's character image: re-anchored to its centre at the same height, WideAspect wide,
        /// so the character keeps its size and the sides open up past the image's old edges.
        bool FindHomeImage()
        {
            if (_home == null) return false;
            foreach (var ri in _home.GetComponentsInChildren<RawImage>(true))
            {
                if (ri.texture != _baseRT) continue;
                var rt = (RectTransform)ri.transform;
                float w = rt.rect.width, h = rt.rect.height;
                if (ri.TryGetComponent<AspectRatioFitter>(out var fit)) fit.enabled = false;
                rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(h * WideAspect, h);
                // The image is also the tap-to-Studio button and the drag-to-turn area: keep the
                // hit area where it was, only the picture grows.
                float pad = Mathf.Max(0f, (h * WideAspect - w) * 0.5f);
                ri.raycastPadding = new Vector4(pad, 0f, pad, 0f);
                _homeImage = ri;
                return true;
            }
            return false;
        }

        /// Review aid: raise and hold the gun until <see cref="EndHold"/> (no firing).
        public void Hold() { _hold = true; if (!_busy) StartCoroutine(Show()); }
        public void EndHold() => _hold = false;

        IEnumerator Show()
        {
            _busy = true;
            var data = Equipped();
            if (data == null || data.weaponPrefab == null) { Finish(); yield break; }
            MakeGun(data);

            // The pistol aim clip hunches the chibi over the gun; a two-hand stance reads better for
            // every gun, the left hand supporting a pistol from below.
            _animator.CrossFadeInFixedTime("Rifle", 0.3f);
            yield return Blend(0.35f, 1f);
            while (_hold) yield return null;

            int shots = Mathf.RoundToInt(Random.Range(shotsPerBurst.x, shotsPerBurst.y));
            float gap = Mathf.Clamp(1f / Mathf.Max(0.5f, data.fireRate), 0.08f, 0.35f);
            for (int i = 0; i < shots && HomeOnTop(); i++)
            {
                _kick = 1f;
                if (_flash != null) _flash.Play(true);
                yield return new WaitForSecondsRealtime(gap);
            }
            yield return new WaitForSecondsRealtime(0.5f);

            _animator.CrossFadeInFixedTime("Idle", 0.35f);
            yield return Blend(0.35f, 0f);
            Finish();
        }

        IEnumerator Blend(float seconds, float to)
        {
            float from = _raise;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                _raise = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
                yield return null;
            }
            _raise = to;
        }

        void Finish()
        {
            if (_gun != null) _gun.SetActive(false);
            _raise = 0f; _kick = 0f;
            _next = Time.unscaledTime + Random.Range(interval.x, interval.y);
            _busy = false;
        }

        static WeaponData Equipped()
        {
            var all = WeaponCatalog.Active != null ? WeaponCatalog.Active.AllData() : null;
            return LoadoutState.Resolve(PlayerProfile.EquippedWeaponId, all);
        }

        void MakeGun(WeaponData data)
        {
            if (_gunData != data)
            {
                if (_gun != null) Destroy(_gun);
                _gun = Instantiate(data.weaponPrefab);
                _gun.name = "ShowcaseGun";
                foreach (var c in _gun.GetComponentsInChildren<Collider>(true)) c.enabled = false;
                _gun.transform.localScale = data.gripLocalScale;
                var grips = _gun.GetComponentInChildren<WeaponGripPoints>(true);
                // Hand targets where the run puts the hands (authored root positions win).
                _gripR = Target("GripR", data.useAuthoredGripPositions ? data.rightHandGripRootPosition : Local(grips?.RightHandGrip));
                var left = data.twoHanded && data.useAuthoredGripPositions ? data.leftHandGripRootPosition : Local(grips?.LeftHandGrip);
                _gripL = grips != null && grips.LeftHandGrip != null ? Target("GripL", left) : null;
                _flash = null;
                var muzzle = grips != null ? grips.MuzzlePoint : null;
                if (data.muzzleFlashPrefab != null && muzzle != null)
                {
                    _flash = Instantiate(data.muzzleFlashPrefab, muzzle);
                    _flash.transform.localPosition = Vector3.zero;
                    _flash.transform.localRotation = Quaternion.identity;
                    var main = _flash.main; main.playOnAwake = false; main.useUnscaledTime = true;
                }
                foreach (var t in _gun.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = _animator.gameObject.layer;
                _gunData = data;
            }
            _gun.SetActive(true);
            Place();
        }

        Vector3 Local(Transform t) => t != null ? _gun.transform.InverseTransformPoint(t.position) : Vector3.zero;

        Transform Target(string n, Vector3 local)
        {
            var t = new GameObject(n).transform;
            t.SetParent(_gun.transform, false);
            t.localPosition = local;
            return t;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            _kick = Mathf.MoveTowards(_kick, 0f, dt * 9f);
            _yaw = Mathf.Lerp(_yaw, _raise * TurnYaw, 1f - Mathf.Exp(-6f * dt));
            if (_pivot != null) _pivot.localRotation = _pivotRest * Quaternion.Euler(0f, _yaw, 0f);
            bool shown = _gun != null && _gun.activeSelf;
            if (shown) Place();
            if (_ik != null)
            {
                _ik.weight = shown ? _raise : 0f;
                _ik.right = shown ? _gripR : null;
                _ik.left = shown ? _gripL : null;
            }
        }

        /// Level, pointing where the character faces, its right grip in front of the right shoulder
        /// (further out for a pistol held at arm's length). Each shot kicks it back and up with a
        /// little random shake; it springs back.
        void Place()
        {
            // The aim clips turn the shoulders side-on; the aim itself is the character's forward.
            var face = _animator.transform.forward; face.y = 0f; face.Normalize();
            var side = Vector3.Cross(Vector3.up, face);
            bool rifle = _gunData.twoHanded;
            var wrist = _shoulderR.position + face * _arm * (rifle ? 0.55f : 0.75f)
                        + Vector3.down * _arm * (rifle ? 0.35f : 0.3f) - side * _arm * (rifle ? 0.1f : 0.25f);

            float k = _kick * _kick;
            var rot = Quaternion.LookRotation(face, Vector3.up) * Quaternion.Euler(-10f * k, Random.Range(-2f, 2f) * k, Random.Range(-2f, 2f) * k)
                      * Quaternion.Euler(_gunData.gripLocalEuler);
            var shake = (side * Random.Range(-1f, 1f) + Vector3.up * Random.Range(-1f, 1f)) * 0.006f * k;
            var grip = Vector3.Scale(_gripR.localPosition, _gunData.gripLocalScale);
            _gun.transform.rotation = rot;
            _gun.transform.position = wrist - rot * grip - face * 0.05f * k + shake;
        }

        void OnDisable()
        {
            StopAllCoroutines();
            if (_gun != null) _gun.SetActive(false);
            if (_pivot != null) _pivot.localRotation = _pivotRest;
            if (_ik != null) _ik.weight = 0f;
            _yaw = 0f; _raise = 0f; _busy = false; _hold = false;
            if (_animator != null && _animator.runtimeAnimatorController != null && _animator.isActiveAndEnabled)
                _animator.Play("Idle", 0, 0f);
        }

        void OnDestroy()
        {
            if (_gun != null) Destroy(_gun);
            if (_wideRT != null) { if (_cam != null && _cam.targetTexture == _wideRT) _cam.targetTexture = _baseRT; _wideRT.Release(); Destroy(_wideRT); }
        }
    }
}
