using UnityEngine;
using Robogee.Player;

namespace Robogee.Combat
{
    /// <summary>
    /// Toggle lock-on: press LockOn once to lock nearest enemy, press again to clear.
    /// Auto-clears on death / out of range. Does not change A/B/C motor stats.
    /// </summary>
    public class UnitLockOn : MonoBehaviour, IAimLockProvider
    {
        [SerializeField] float acquireRange = 36f;
        [SerializeField] float loseRange = 44f;
        [SerializeField] float aimHeight = 1.0f;
        [SerializeField] float markerHeight = 2.25f;
        [SerializeField] CombatantCore selfCore;
        [SerializeField] MonoBehaviour inputSourceBehaviour;

        IUnitInputSource _input;
        CombatantCore _target;

        public bool IsLocked => _target != null && !_target.IsDead;
        public CombatantCore CurrentTarget => IsLocked ? _target : null;

        void Awake()
        {
            if (selfCore == null)
                selfCore = GetComponent<CombatantCore>();
            ResolveInput();
        }

        void ResolveInput()
        {
            if (inputSourceBehaviour is IUnitInputSource typed)
            {
                _input = typed;
                return;
            }

            var human = GetComponent<UnitPlayerInput>();
            if (human != null)
            {
                _input = human;
                inputSourceBehaviour = human;
            }
        }

        void Update()
        {
            if (_input == null)
                ResolveInput();

            UnitInputFrame frame = _input != null ? _input.Current : default;
            if (frame.LockOnPressed)
                Toggle();

            if (_target == null)
                return;

            if (_target.IsDead)
            {
                Clear();
                return;
            }

            float dist = Vector3.Distance(transform.position, _target.transform.position);
            if (dist > loseRange)
                Clear();
        }

        public void Toggle()
        {
            if (IsLocked)
            {
                Clear();
                return;
            }

            CombatantCore best = FindBestTarget();
            if (best != null)
                _target = best;
        }

        public void Clear()
        {
            _target = null;
        }

        public bool TryGetAimPoint(out Vector3 worldPoint)
        {
            return TryGetLockedAimPoint(out worldPoint);
        }

        /// <summary>World point above the locked target (for HUD diamond / reticle).</summary>
        public bool TryGetMarkerPoint(out Vector3 worldPoint)
        {
            if (!IsLocked)
            {
                worldPoint = default;
                return false;
            }

            worldPoint = GetAboveHeadPoint(_target.transform);
            return true;
        }

        public bool TryGetLockedAimPoint(out Vector3 worldPoint)
        {
            if (!IsLocked)
            {
                worldPoint = default;
                return false;
            }

            // Chest aim from mesh bounds (not a fixed 1m — gaikotu is taller).
            if (TryGetBounds(_target.transform, out Bounds b))
            {
                worldPoint = new Vector3(b.center.x, Mathf.Lerp(b.min.y, b.max.y, 0.62f), b.center.z);
                return true;
            }

            worldPoint = _target.transform.position + Vector3.up * aimHeight;
            return true;
        }

        static Vector3 GetAboveHeadPoint(Transform t)
        {
            if (TryGetBounds(t, out Bounds b))
                return new Vector3(b.center.x, b.max.y + 0.45f, b.center.z);
            return t.position + Vector3.up * 2.6f;
        }

        static bool TryGetBounds(Transform t, out Bounds bounds)
        {
            var rends = t.GetComponentsInChildren<Renderer>();
            if (rends == null || rends.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++)
            {
                if (rends[i] != null)
                    bounds.Encapsulate(rends[i].bounds);
            }

            return bounds.size.sqrMagnitude > 0.0001f;
        }

        CombatantCore FindBestTarget()
        {
            CombatantCore best = null;
            float bestSqr = acquireRange * acquireRange;
            var cores = FindObjectsByType<CombatantCore>(FindObjectsSortMode.None);
            for (int i = 0; i < cores.Length; i++)
            {
                var c = cores[i];
                if (c == null || c.IsDead)
                    continue;
                if (selfCore != null && c.TeamId == selfCore.TeamId)
                    continue;

                float sqr = (c.transform.position - transform.position).sqrMagnitude;
                if (sqr > bestSqr)
                    continue;
                bestSqr = sqr;
                best = c;
            }

            return best;
        }
    }
}
