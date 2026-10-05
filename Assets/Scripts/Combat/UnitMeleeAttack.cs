using UnityEngine;
using Robogee.Player;

namespace Robogee.Combat
{
    /// <summary>α melee: Attack → forward sweep damage (reliable vs CharacterController).</summary>
    public class UnitMeleeAttack : MonoBehaviour
    {
        [SerializeField] CombatantCore core;
        [SerializeField] MonoBehaviour inputSourceBehaviour;
        [SerializeField] Transform attackOrigin;
        [SerializeField] float damage = 18f;
        [SerializeField] float range = 3.2f;
        [SerializeField] float radius = 1.1f;
        [SerializeField] float cooldown = 0.4f;
        [SerializeField] LayerMask hitMask = ~0;

        IUnitInputSource _input;
        float _nextAttack;
        readonly RaycastHit[] _hits = new RaycastHit[24];
        readonly Collider[] _overlap = new Collider[24];

        void Awake()
        {
            if (core == null)
                core = GetComponent<CombatantCore>();
            ResolveInput();
            if (attackOrigin == null)
            {
                var cam = GetComponentInChildren<Camera>();
                attackOrigin = cam != null ? cam.transform : transform;
            }
            if (core != null)
                core.EnsureHurtbox();
        }

        void ResolveInput()
        {
            if (inputSourceBehaviour is IUnitInputSource typed)
            {
                _input = typed;
                return;
            }

            var behaviours = GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is NpcChaseBrain npc && npc.enabled)
                {
                    _input = npc;
                    return;
                }
            }

            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IUnitInputSource src && behaviours[i].enabled)
                {
                    _input = src;
                    return;
                }
            }

            _input = GetComponent<UnitPlayerInput>();
        }

        void Update()
        {
            if (core != null && core.IsDead)
                return;

            ResolveInput();
            if (_input == null)
                return;
            if (!_input.Current.AttackPressed)
                return;
            if (Time.time < _nextAttack)
                return;

            _nextAttack = Time.time + cooldown;
            Fire();
        }

        public void ForceAttack()
        {
            if (core != null && core.IsDead)
                return;
            if (Time.time < _nextAttack)
                return;
            _nextAttack = Time.time + cooldown;
            Fire();
        }

        void Fire()
        {
            Transform originTf = attackOrigin != null ? attackOrigin : transform;
            Vector3 origin = originTf.position;
            Vector3 dir = originTf.forward;
            // Prefer chest-height sweep so camera pitch still hits standing foes.
            if (attackOrigin != null && attackOrigin.GetComponent<Camera>() != null)
            {
                origin = transform.position + Vector3.up * 1.0f;
                dir = attackOrigin.forward;
                dir.y *= 0.35f;
                if (dir.sqrMagnitude < 0.001f)
                    dir = transform.forward;
                dir.Normalize();
            }

            CombatantCore best = null;
            float bestDist = float.MaxValue;

            int castCount = Physics.SphereCastNonAlloc(
                origin, radius, dir, _hits, range, hitMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < castCount; i++)
                Consider(_hits[i].collider, origin, ref best, ref bestDist);

            // Close-range puddle in case cast starts inside collider.
            int overCount = Physics.OverlapSphereNonAlloc(
                origin + dir * (radius * 0.5f), radius * 1.15f, _overlap, hitMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < overCount; i++)
                Consider(_overlap[i], origin, ref best, ref bestDist);

            if (best != null)
                best.ApplyDamage(damage, core);
        }

        void Consider(Collider col, Vector3 origin, ref CombatantCore best, ref float bestDist)
        {
            if (col == null)
                return;
            if (col.transform.root == transform.root)
                return;

            var target = col.GetComponentInParent<CombatantCore>();
            if (target == null || target.IsDead)
                return;
            if (core != null && target.TeamId == core.TeamId)
                return;

            float d = (target.transform.position - origin).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                best = target;
            }
        }
    }
}
