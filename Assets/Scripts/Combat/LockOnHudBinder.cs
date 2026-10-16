using UnityEngine;
using UnityEngine.UI;

namespace Robogee.Combat
{
    /// <summary>
    /// Binds lock-on state to Canvas Prefab. Child: Reticle.
    /// </summary>
    public class LockOnHudBinder : MonoBehaviour
    {
        [SerializeField] GameObject hudPrefab;
        [SerializeField] bool instantiateIfMissing = true;
        [SerializeField] UnitLockOn lockOn;
        [SerializeField] Camera worldCamera;
        [SerializeField] RectTransform reticle;
        [SerializeField] CanvasGroup canvasGroup;

        [Header("絆 lock colors")]
        [SerializeField] Color blueLock = new Color(0.3f, 0.7f, 1f, 0.95f);
        [SerializeField] Color redLock = new Color(1f, 0.2f, 0.15f, 0.95f);
        [SerializeField] Color yellowLock = new Color(1f, 0.85f, 0.2f, 1f);
        [Tooltip("Distance label multiplier: 1 unit = 10m of an 18m MS.")]
        [SerializeField] float metersPerUnit = 10f;

        GameObject _instance;
        Graphic _reticleGraphic;
        Graphic _rimGraphic;
        Graphic _labelGraphic;
        Text _distanceText;

        public void Bind(UnitLockOn source)
        {
            lockOn = source;
            EnsureInstance();
            TryAutoWire();
            if (_instance != null)
                _instance.SetActive(true);
        }

        public void HideForSelect()
        {
            lockOn = null;
            if (_instance != null)
                _instance.SetActive(false);
        }

        void Awake()
        {
            if (lockOn == null)
                lockOn = GetComponentInParent<UnitLockOn>();
            EnsureInstance();
            TryAutoWire();
        }

        void LateUpdate()
        {
            if (lockOn == null || reticle == null)
                return;

            bool on = lockOn.TryGetMarkerPoint(out Vector3 marker);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = on ? 1f : 0f;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
            else
            {
                reticle.gameObject.SetActive(on);
            }

            if (!on)
                return;

            if (worldCamera == null)
                worldCamera = Camera.main;
            if (worldCamera == null)
                return;

            Vector3 screen = worldCamera.WorldToScreenPoint(marker);
            if (screen.z < 0.05f)
            {
                if (canvasGroup != null)
                    canvasGroup.alpha = 0f;
                return;
            }

            reticle.position = screen;
            ApplyLockVisuals();
        }

        void ApplyLockVisuals()
        {
            Color state = lockOn.State == UnitLockOn.LockState.Red ? redLock : blueLock;
            bool melee = lockOn.IsInMeleeRange;

            if (_reticleGraphic != null)
                _reticleGraphic.color = state;
            if (_rimGraphic != null)
                _rimGraphic.color = melee ? yellowLock : state;
            if (_labelGraphic != null)
                _labelGraphic.color = melee ? yellowLock : state;

            if (_distanceText != null)
            {
                _distanceText.text = $"{lockOn.DisplayDistance * metersPerUnit:0}m";
                _distanceText.color = melee ? yellowLock : state;
            }
        }

        void EnsureInstance()
        {
            if (_instance != null)
                return;
            if (!instantiateIfMissing)
                return;

            if (hudPrefab == null)
                hudPrefab = Resources.Load<GameObject>("UI/LockOnHud");

            if (hudPrefab == null)
            {
                Debug.LogWarning("[LockOnHud] LockOnHud prefab missing.", this);
                return;
            }

            var existing = GameObject.Find("LockOnHudCanvas");
            if (existing != null)
            {
                _instance = existing;
                return;
            }

            _instance = Instantiate(hudPrefab);
            _instance.name = "LockOnHudCanvas";
        }

        void TryAutoWire()
        {
            Transform root = _instance != null ? _instance.transform : transform;
            if (reticle == null)
            {
                var t = FindDeep(root, "Reticle");
                if (t != null)
                    reticle = t as RectTransform ?? t.GetComponent<RectTransform>();
            }

            if (canvasGroup == null && _instance != null)
                canvasGroup = _instance.GetComponent<CanvasGroup>();

            if (reticle == null)
                return;

            if (_reticleGraphic == null)
                _reticleGraphic = reticle.GetComponent<Graphic>();
            if (_rimGraphic == null)
                _rimGraphic = FindDeep(root, "Rim")?.GetComponent<Graphic>();
            if (_labelGraphic == null)
                _labelGraphic = FindDeep(root, "LockLabel")?.GetComponent<Graphic>();
            if (_distanceText == null)
                _distanceText = EnsureDistanceText(reticle);
        }

        static Text EnsureDistanceText(RectTransform parent)
        {
            var existing = parent.Find("Distance");
            if (existing != null && existing.TryGetComponent(out Text found))
                return found;

            var go = new GameObject("Distance", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -40f);
            rt.sizeDelta = new Vector2(120f, 28f);

            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 20;
            text.alignment = TextAnchor.UpperCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root == null)
                return null;
            if (root.name == name)
                return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var hit = FindDeep(root.GetChild(i), name);
                if (hit != null)
                    return hit;
            }

            return null;
        }
    }
}
