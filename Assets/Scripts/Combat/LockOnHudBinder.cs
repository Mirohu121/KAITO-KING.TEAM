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

        GameObject _instance;

        public void Bind(UnitLockOn source)
        {
            lockOn = source;
            EnsureInstance();
            TryAutoWire();
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

            bool on = false;
            Vector3 marker = default;
            if (lockOn.IsLocked && lockOn.TryGetMarkerPoint(out marker))
                on = true;

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
