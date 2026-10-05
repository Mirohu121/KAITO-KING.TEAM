using UnityEngine;
using Robogee.Player;

namespace Robogee.Combat
{
    /// <summary>
    /// Chase / strafe / dash-in / melee brain feeding <see cref="IUnitInputSource"/>.
    /// </summary>
    [DefaultExecutionOrder(-90)]
    public class NpcChaseBrain : MonoBehaviour, IUnitInputSource
    {
        [SerializeField] Transform target;
        [SerializeField] float preferDistance = 1.8f;
        [SerializeField] float attackRange = 3.0f;
        [SerializeField] float dashDistance = 7f;
        [SerializeField] float attackCooldown = 0.55f;
        [SerializeField] float strafeSpeed = 0.65f;
        [SerializeField] UnitMeleeAttack melee;

        UnitInputFrame _current;
        float _nextAttack;
        float _strafeSign = 1f;
        float _nextStrafeFlip;
        UnitBMotor _motor;

        public UnitInputFrame Current => _current;

        void Awake()
        {
            _motor = GetComponent<UnitBMotor>();
            if (melee == null)
                melee = GetComponent<UnitMeleeAttack>();

            var human = GetComponent<UnitPlayerInput>();
            if (human != null)
                human.EnableActions = false;

            // NPC should not steal / toggle the global cursor.
            if (_motor != null)
                _motor.SetInputSource(this);
        }

        void Start()
        {
            // Disable cursor lock behaviour side-effects: keep looking via yaw snap instead.
            FindTarget();
            _nextStrafeFlip = Time.time + Random.Range(0.8f, 1.6f);
            _strafeSign = Random.value < 0.5f ? -1f : 1f;
        }

        void FindTarget()
        {
            foreach (var c in FindObjectsByType<CombatantCore>(FindObjectsSortMode.None))
            {
                if (c != null && c.TeamId == CombatantCore.Team.Player && !c.IsDead)
                {
                    target = c.transform;
                    return;
                }
            }
        }

        void Update()
        {
            _current = default;
            if (target == null)
            {
                FindTarget();
                if (target == null)
                    return;
            }

            var targetCore = target.GetComponent<CombatantCore>();
            if (targetCore != null && targetCore.IsDead)
                return;

            Vector3 to = target.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;

            // Snap face — yaw only so the skeleton never tilts.
            if (dist > 0.05f)
            {
                float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }

            if (Time.time >= _nextStrafeFlip)
            {
                _strafeSign = -_strafeSign;
                _nextStrafeFlip = Time.time + Random.Range(0.7f, 1.8f);
            }

            float moveZ = 0f;
            float moveX = 0f;

            if (dist > preferDistance + 0.35f)
            {
                moveZ = 1f;
                // Circle while closing so it isn't a pure zombie walk.
                moveX = _strafeSign * strafeSpeed * 0.45f;
            }
            else if (dist < preferDistance * 0.55f)
            {
                moveZ = -0.7f;
                moveX = _strafeSign * strafeSpeed;
            }
            else
            {
                // In the pocket: strafe and pressure.
                moveX = _strafeSign * strafeSpeed;
                moveZ = 0.15f;
            }

            _current.MoveX = moveX;
            _current.MoveY = moveZ;

            // Dash in when far.
            if (dist > dashDistance)
                _current.DashHeld = true;

            // Occasional hop while closing.
            if (dist > 4f && dist < 10f && Random.value < 0.01f)
                _current.JumpPressed = true;

            if (dist <= attackRange && Time.time >= _nextAttack)
            {
                _nextAttack = Time.time + attackCooldown;
                _current.AttackPressed = true;
            }
        }
    }
}
