using System;
using UnityEngine;

namespace Robogee.Combat
{
    /// <summary>Core HP. Zero core = destroyed (α win condition).</summary>
    public class CombatantCore : MonoBehaviour
    {
        public enum Team
        {
            Player = 0,
            Enemy = 1
        }

        [SerializeField] Team team = Team.Player;
        [SerializeField] float maxCoreHp = 100f;
        [SerializeField] float currentCoreHp = 100f;
        [SerializeField] bool destroyRootOnDeath = true;
        [SerializeField] float destroyDelay = 0.4f;

        public Team TeamId => team;
        public float MaxCoreHp => maxCoreHp;
        public float CurrentCoreHp => currentCoreHp;
        public float CoreNormalized => maxCoreHp > 0.0001f ? Mathf.Clamp01(currentCoreHp / maxCoreHp) : 0f;
        public bool IsDead { get; private set; }

        public event Action<CombatantCore> Died;
        public event Action<CombatantCore, float, float> Damaged;

        public void Configure(Team newTeam, float hp)
        {
            team = newTeam;
            maxCoreHp = Mathf.Max(1f, hp);
            currentCoreHp = maxCoreHp;
            IsDead = false;
        }

        public void EnsureHurtbox()
        {
            if (GetComponent<CapsuleCollider>() != null)
                return;

            var capsule = gameObject.AddComponent<CapsuleCollider>();
            var cc = GetComponent<CharacterController>();
            if (cc != null)
            {
                capsule.height = cc.height;
                capsule.radius = Mathf.Max(0.35f, cc.radius);
                capsule.center = cc.center;
            }
            else
            {
                capsule.height = 2f;
                capsule.radius = 0.5f;
                capsule.center = Vector3.zero;
            }
        }

        public float ApplyDamage(float amount, CombatantCore attacker)
        {
            if (IsDead || amount <= 0f)
                return 0f;
            if (attacker != null && attacker.TeamId == team)
                return 0f;

            float before = currentCoreHp;
            currentCoreHp = Mathf.Max(0f, currentCoreHp - amount);
            float applied = before - currentCoreHp;
            Damaged?.Invoke(this, applied, currentCoreHp);

            if (currentCoreHp <= 0f)
                Die();

            return applied;
        }

        void Die()
        {
            if (IsDead)
                return;
            IsDead = true;
            Died?.Invoke(this);

            foreach (var behaviour in GetComponentsInChildren<MonoBehaviour>())
            {
                if (behaviour == this)
                    continue;
                behaviour.enabled = false;
            }

            var cc = GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = false;

            transform.localScale *= 1.12f;
            if (destroyRootOnDeath)
                Destroy(gameObject, destroyDelay);
        }
    }
}
