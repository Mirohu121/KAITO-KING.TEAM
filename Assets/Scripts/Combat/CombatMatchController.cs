using UnityEngine;
using Robogee.Player;
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

        //＃追記＃試合終了後のUI表示用のカメラを入れる
        //Finish()に処理を追加
        [SerializeField] private Camera resultCamera;


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
            ResolveResultCamera();
            SetResultCameraActive(false);
            if (player != null)
                player.Died += OnDied;
            if (enemy != null)
                enemy.Died += OnDied;
        }

        /// <summary>Clear match state so unit select can run again without a full scene reload.</summary>
        public void ResetForSelect()
        {
            Unbind();
            player = null;
            enemy = null;
            _ended = false;
            _result = null;
            _timeLeft = matchDurationSeconds;
            SetResultCameraActive(false);
            Time.timeScale = 1f;
        }

        void ResolveResultCamera()
        {
            if (resultCamera != null)
                return;
            var go = GameObject.Find("ResultCamera");
            if (go != null)
                resultCamera = go.GetComponent<Camera>();
        }

        void SetResultCameraActive(bool active)
        {
            ResolveResultCamera();
            if (resultCamera != null)
                resultCamera.gameObject.SetActive(active);
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
            // 追加：試合終了時にFuelゲージを非表示にする
            FuelGaugeHud.HideAll();

            //追記：試合終了後のUI表示用のカメラを有効化する
            SetResultCameraActive(true);
        }

        void OnDestroy()
        {
            if (_ended)
                Time.timeScale = 1f;
        }
    }
}
