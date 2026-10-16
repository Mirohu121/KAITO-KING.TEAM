using UnityEngine;
using Robogee.Player;

namespace Robogee.Combat
{
    /// <summary>
    /// 戦場の絆 lock-on:
    /// 青ロック — automatic when an enemy is inside range + FCS cone and visible; lost behind cover.
    /// 赤ロック — LockOn while blue; the motor auto-faces it, survives cover, drops when out of range.
    /// 黄ロック — display only: target is within melee reach (melee then homes on it).
    /// ロック送り — LockSwitch hops to the next enemy in range + FCS, keeping red/blue.
    /// Does not change A/B/C motor stats.
    /// </summary>
    public class UnitLockOn : MonoBehaviour, IAimLockProvider
    {
        public enum LockState
        {
            None,
            Blue,
            Red
        }

        [Header("Range (units; ×10 = 絆 meters)")]
        [Tooltip("赤ロック距離. 近距離戦型 ≈ 210m.")]
        [SerializeField] float lockRange = 22f;
        [Tooltip("Red lock survives a little past lockRange so it doesn't flicker at the edge.")]
        [SerializeField] float releaseRangeScale = 1.08f;
        [Tooltip("黄ロック: melee homes on the target inside this distance.")]
        [SerializeField] float meleeLockRange = 4.2f;

        [Header("FCS")]
        [Tooltip("Half-angle of the lockable cone in front of the body.")]
        [SerializeField] float fcsHalfAngle = 35f;
        [SerializeField] float fcsVerticalHalfAngle = 50f;
        [SerializeField] bool blueNeedsLineOfSight = true;
        [SerializeField] LayerMask sightMask = ~0;

        [Header("Aim")]
        [SerializeField] float aimHeight = 1.0f;
        [SerializeField] CombatantCore selfCore;
        [SerializeField] MonoBehaviour inputSourceBehaviour;

        IUnitInputSource _input;
        Transform _eye;
        CombatantCore _red;
        CombatantCore _blue;
        readonly RaycastHit[] _sightHits = new RaycastHit[16];

        public LockState State => IsLocked ? LockState.Red : IsAlive(_blue) ? LockState.Blue : LockState.None;
        public bool IsLocked => IsAlive(_red);
        public CombatantCore CurrentTarget => IsLocked ? _red : null;
        public CombatantCore BlueTarget => IsAlive(_blue) ? _blue : null;
        /// <summary>Red if locked, else blue. What the HUD marker follows.</summary>
        public CombatantCore DisplayTarget => IsLocked ? _red : BlueTarget;
        public bool IsInMeleeRange => DisplayTarget != null && DistanceTo(DisplayTarget) <= meleeLockRange;
        public float DisplayDistance => DisplayTarget != null ? DistanceTo(DisplayTarget) : 0f;
        public float LockRange => lockRange;

        void Awake()
        {
            if (selfCore == null)
                selfCore = GetComponent<CombatantCore>();
            ResolveInput();
            ResolveEye();
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

        void ResolveEye()
        {
            var cam = GetComponentInChildren<Camera>();
            _eye = cam != null ? cam.transform : null;
        }

        void Update()
        {
            if (_input == null)
                ResolveInput();
            if (_eye == null)
                ResolveEye();

            if (selfCore != null && selfCore.IsDead)
            {
                Clear();
                _blue = null;
                return;
            }

            UpdateRed();
            UpdateBlue();

            UnitInputFrame frame = _input != null ? _input.Current : default;
            if (frame.LockOnPressed)
                Toggle();
            if (frame.LockSwitchPressed)
                SwitchTarget();
        }

        void UpdateRed()
        {
            if (_red == null)
                return;
            if (_red.IsDead || DistanceTo(_red) > lockRange * releaseRangeScale)
                _red = null;
        }

        void UpdateBlue()
        {
            if (IsAlive(_blue) && IsBlueValid(_blue))
                return;
            _blue = FindBestBlue();
        }

        /// <summary>Red ↔ off. Red needs a blue target first, as in 絆.</summary>
        public void Toggle()
        {
            if (IsLocked)
            {
                Clear();
                return;
            }

            if (BlueTarget != null)
                _red = _blue;
        }

        public void Clear()
        {
            _red = null;
        }

        /// <summary>ロック送り: next enemy clockwise in range + FCS. Red stays red.</summary>
        public void SwitchTarget()
        {
            CombatantCore current = DisplayTarget;
            float currentAngle = current != null ? SignedYaw(current) : -999f;

            CombatantCore next = null;
            float nextAngle = float.MaxValue;
            CombatantCore wrap = null;
            float wrapAngle = float.MaxValue;

            var cores = FindObjectsByType<CombatantCore>(FindObjectsSortMode.None);
            for (int i = 0; i < cores.Length; i++)
            {
                var c = cores[i];
                if (c == current || !IsEnemy(c))
                    continue;
                if (DistanceTo(c) > lockRange || !InFcs(c))
                    continue;

                float a = SignedYaw(c);
                if (a > currentAngle && a < nextAngle)
                {
                    next = c;
                    nextAngle = a;
                }
                if (a < wrapAngle)
                {
                    wrap = c;
                    wrapAngle = a;
                }
            }

            CombatantCore pick = next != null ? next : wrap;
            if (pick == null)
                return;

            if (IsLocked)
                _red = pick;
            else
                _blue = pick;
        }

        public bool TryGetAimPoint(out Vector3 worldPoint)
        {
            return TryGetLockedAimPoint(out worldPoint);
        }

        /// <summary>Red lock only — this is what drives the motor's auto-turn.</summary>
        public bool TryGetLockedAimPoint(out Vector3 worldPoint)
        {
            if (!IsLocked)
            {
                worldPoint = default;
                return false;
            }

            worldPoint = GetAimPoint(_red);
            return true;
        }

        /// <summary>黄ロック target for melee homing (red or blue).</summary>
        public bool TryGetMeleeAssistPoint(out Vector3 worldPoint)
        {
            if (!IsInMeleeRange)
            {
                worldPoint = default;
                return false;
            }

            worldPoint = GetAimPoint(DisplayTarget);
            return true;
        }

        /// <summary>World point above the displayed target (HUD marker).</summary>
        public bool TryGetMarkerPoint(out Vector3 worldPoint)
        {
            var target = DisplayTarget;
            if (target == null)
            {
                worldPoint = default;
                return false;
            }

            worldPoint = GetAboveHeadPoint(target.transform);
            return true;
        }

        CombatantCore FindBestBlue()
        {
            CombatantCore best = null;
            float bestScore = float.MaxValue;
            var cores = FindObjectsByType<CombatantCore>(FindObjectsSortMode.None);
            for (int i = 0; i < cores.Length; i++)
            {
                var c = cores[i];
                if (!IsEnemy(c) || !IsBlueValid(c))
                    continue;

                // Closest to the sight centre wins; distance breaks ties.
                float score = Mathf.Abs(SignedYaw(c)) + DistanceTo(c) * 0.5f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = c;
                }
            }

            return best;
        }

        bool IsBlueValid(CombatantCore c)
        {
            if (DistanceTo(c) > lockRange)
                return false;
            if (!InFcs(c))
                return false;
            return !blueNeedsLineOfSight || HasLineOfSight(c);
        }

        bool IsEnemy(CombatantCore c)
        {
            if (!IsAlive(c))
                return false;
            if (c.transform.root == transform.root)
                return false;
            return selfCore == null || c.TeamId != selfCore.TeamId;
        }

        static bool IsAlive(CombatantCore c) => c != null && !c.IsDead;

        float DistanceTo(CombatantCore c) => Vector3.Distance(transform.position, c.transform.position);

        Vector3 EyePosition => _eye != null ? _eye.position : transform.position + Vector3.up * 1.5f;

        float SignedYaw(CombatantCore c)
        {
            Vector3 to = c.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.0001f)
                return 0f;
            return Vector3.SignedAngle(transform.forward, to, Vector3.up);
        }

        bool InFcs(CombatantCore c)
        {
            if (Mathf.Abs(SignedYaw(c)) > fcsHalfAngle)
                return false;

            Vector3 to = GetAimPoint(c) - EyePosition;
            float flat = new Vector2(to.x, to.z).magnitude;
            float pitch = Mathf.Atan2(to.y, Mathf.Max(0.001f, flat)) * Mathf.Rad2Deg;
            return Mathf.Abs(pitch) <= fcsVerticalHalfAngle;
        }

        bool HasLineOfSight(CombatantCore c)
        {
            Vector3 from = EyePosition;
            Vector3 to = GetAimPoint(c);
            Vector3 dir = to - from;
            float dist = dir.magnitude;
            if (dist < 0.01f)
                return true;

            int count = Physics.RaycastNonAlloc(
                from, dir / dist, _sightHits, dist, sightMask, QueryTriggerInteraction.Ignore);
            Transform selfRoot = transform.root;
            Transform targetRoot = c.transform.root;
            for (int i = 0; i < count; i++)
            {
                Transform hitRoot = _sightHits[i].collider.transform.root;
                if (hitRoot == selfRoot || hitRoot == targetRoot)
                    continue;
                return false;
            }

            return true;
        }

        Vector3 GetAimPoint(CombatantCore c)
        {
            // Chest aim from mesh bounds (not a fixed 1m — gaikotu is taller).
            if (TryGetBounds(c.transform, out Bounds b))
                return new Vector3(b.center.x, Mathf.Lerp(b.min.y, b.max.y, 0.62f), b.center.z);
            return c.transform.position + Vector3.up * aimHeight;
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
    }
}
