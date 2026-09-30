using UnityEngine;

namespace Robogee.Combat
{
    /// <summary>
    /// α match: 1v1, core kill wins; on time-over higher remaining core HP wins.
    /// </summary>
    public class CombatMatchController : MonoBehaviour
    {
        [SerializeField] CombatantCore player;
        [SerializeField] CombatantCore enemy;
        [SerializeField] float matchDurationSeconds = 180f;
        [SerializeField] bool freezeTimeOnEnd = true;

        float _timeLeft;
        bool _ended;
        string _result;

        public string ResultText => _result;
        public bool HasEnded => _ended;
        public float TimeLeft => _timeLeft;
        public string TimerDisplay
        {
            get
            {
                int t = Mathf.Max(0, Mathf.CeilToInt(_timeLeft));
                return $"{t / 60:00}:{t % 60:00}";
            }
        }

        public void Bind(CombatantCore playerCore, CombatantCore enemyCore)
        {
            Unbind();
            player = playerCore;
            enemy = enemyCore;
            _ended = false;
            _result = null;
            _timeLeft = matchDurationSeconds;
            if (player != null)
                player.Died += OnDied;
            if (enemy != null)
                enemy.Died += OnDied;
        }

        void OnDisable() => Unbind();

        void Unbind()
        {
            if (player != null)
                player.Died -= OnDied;
            if (enemy != null)
                enemy.Died -= OnDied;
        }

        void Update()
        {
            if (_ended)
                return;
            if (player == null || enemy == null)
                return;

            _timeLeft -= Time.deltaTime;
            if (_timeLeft <= 0f)
                EndOnTimeOver();
        }

        void OnDied(CombatantCore dead)
        {
            if (_ended)
                return;

            if (dead.TeamId == CombatantCore.Team.Player)
                Finish("DEFEAT — コア破壊");
            else
                Finish("VICTORY — 敵コア破壊");
        }

        void EndOnTimeOver()
        {
            float p = player != null ? player.CurrentCoreHp : 0f;
            float e = enemy != null ? enemy.CurrentCoreHp : 0f;
            if (p > e)
                Finish("VICTORY — 残コア優勢");
            else if (e > p)
                Finish("DEFEAT — 残コア劣勢");
            else
                Finish("DRAW — タイムオーバー");
        }

        void Finish(string result)
        {
            _ended = true;
            _result = result;
            _timeLeft = Mathf.Max(0f, _timeLeft);
            if (freezeTimeOnEnd)
                Time.timeScale = 0.2f;
        }

        void OnDestroy()
        {
            if (_ended)
                Time.timeScale = 1f;
        }
    }
}
