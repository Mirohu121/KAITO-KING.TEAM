using UnityEngine;
using UnityEngine.UI;
using Robogee.Player;

namespace Robogee.Combat
{
    /// <summary>
    /// Binds match state to a Canvas Prefab.
    /// Child names: PlayerHpFill, EnemyHpFill, TimerText, ResultText
    /// </summary>
    public class MatchHudBinder : MonoBehaviour
    {
        [SerializeField] GameObject hudPrefab;
        [SerializeField] bool instantiateIfMissing = true;
        [SerializeField] CombatMatchController match;
        [SerializeField] CombatantCore playerCore;
        [SerializeField] CombatantCore enemyCore;

        [SerializeField] Image playerHpFill;
        [SerializeField] Image enemyHpFill;
        [SerializeField] Text timerText;
        [SerializeField] Text resultText;

        GameObject _instance;

        public void Bind(CombatMatchController matchController, CombatantCore player, CombatantCore enemy)
        {
            match = matchController;
            playerCore = player;
            enemyCore = enemy;
            EnsureInstance();
            TryAutoWire();
            PrepareHp(playerHpFill, new Color(0.3f, 0.85f, 0.4f, 0.95f), fromLeft: true);
            PrepareHp(enemyHpFill, new Color(0.95f, 0.35f, 0.3f, 0.95f), fromLeft: false);
            ApplyReadableStyle();
        }

        void Awake()
        {
            EnsureInstance();
            TryAutoWire();
            PrepareHp(playerHpFill, new Color(0.3f, 0.85f, 0.4f, 0.95f), fromLeft: true);
            PrepareHp(enemyHpFill, new Color(0.95f, 0.35f, 0.3f, 0.95f), fromLeft: false);
            ApplyReadableStyle();
        }

        void ApplyReadableStyle()
        {
            Transform root = _instance != null ? _instance.transform : transform;

            // Keep HP row inside the safe top area (labels were overflowing past y=1).
            SetRect(FindDeep<RectTransform>(root, "PlayerHpLabel"),
                new Vector2(0.02f, 0.905f), new Vector2(0.30f, 0.955f));
            SetRect(FindDeep<RectTransform>(root, "EnemyHpLabel"),
                new Vector2(0.70f, 0.905f), new Vector2(0.98f, 0.955f));
            SetRect(FindDeep<RectTransform>(root, "PlayerHpTrack"),
                new Vector2(0.02f, 0.845f), new Vector2(0.30f, 0.895f));
            SetRect(FindDeep<RectTransform>(root, "PlayerHpFill"),
                new Vector2(0.02f, 0.845f), new Vector2(0.30f, 0.895f));
            SetRect(FindDeep<RectTransform>(root, "EnemyHpTrack"),
                new Vector2(0.70f, 0.845f), new Vector2(0.98f, 0.895f));
            SetRect(FindDeep<RectTransform>(root, "EnemyHpFill"),
                new Vector2(0.70f, 0.845f), new Vector2(0.98f, 0.895f));
            SetRect(FindDeep<RectTransform>(root, "TimerText"),
                new Vector2(0.35f, 0.86f), new Vector2(0.65f, 0.96f));

            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                if (text == null)
                    continue;
                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                switch (text.name)
                {
                    case "TimerText":
                        text.fontSize = 64;
                        break;
                    case "ResultText":
                        text.fontSize = 56;
                        break;
                    case "PlayerHpLabel":
                        text.text = "自機";
                        text.fontSize = 32;
                        text.alignment = TextAnchor.MiddleLeft;
                        break;
                    case "EnemyHpLabel":
                        text.text = "敵";
                        text.fontSize = 32;
                        text.alignment = TextAnchor.MiddleRight;
                        break;
                    default:
                        text.fontSize = Mathf.Max(text.fontSize, 24);
                        break;
                }
            }
        }

        static void SetRect(RectTransform rt, Vector2 amin, Vector2 amax)
        {
            if (rt == null)
                return;
            rt.anchorMin = amin;
            rt.anchorMax = amax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }

        static T FindDeep<T>(Transform root, string name) where T : Component
        {
            if (root == null)
                return null;
            var all = root.GetComponentsInChildren<T>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name)
                    return all[i];
            }
            return null;
        }

        void LateUpdate()
        {
            if (playerHpFill != null && playerCore != null)
                playerHpFill.fillAmount = playerCore.CoreNormalized;
            if (enemyHpFill != null && enemyCore != null)
                enemyHpFill.fillAmount = enemyCore.CoreNormalized;

            if (timerText != null && match != null)
                timerText.text = match.TimerDisplay;

            if (resultText != null && match != null)
            {
                bool show = match.HasEnded && !string.IsNullOrEmpty(match.ResultText);
                resultText.gameObject.SetActive(show);
                if (show)
                    resultText.text = match.ResultText;
            }
        }

        void EnsureInstance()
        {
            if (playerHpFill != null && enemyHpFill != null && timerText != null)
                return;
            if (!instantiateIfMissing)
                return;

            if (hudPrefab == null)
                hudPrefab = Resources.Load<GameObject>("UI/MatchHud");

            if (hudPrefab == null)
            {
                Debug.LogWarning("[MatchHudBinder] MatchHud prefab missing.", this);
                return;
            }

            var existing = GameObject.Find("MatchHudCanvas");
            if (existing != null)
            {
                _instance = existing;
                return;
            }

            _instance = Instantiate(hudPrefab);
            _instance.name = "MatchHudCanvas";
        }

        void TryAutoWire()
        {
            Transform root = _instance != null ? _instance.transform : transform;
            if (playerHpFill == null)
                playerHpFill = FindDeep<Image>(root, "PlayerHpFill");
            if (enemyHpFill == null)
                enemyHpFill = FindDeep<Image>(root, "EnemyHpFill");
            if (timerText == null)
                timerText = FindDeep<Text>(root, "TimerText");
            if (resultText == null)
                resultText = FindDeep<Text>(root, "ResultText");
        }

        static void PrepareHp(Image img, Color color, bool fromLeft)
        {
            if (img == null)
                return;
            if (img.sprite == null)
                img.sprite = FuelGaugeHud.WhiteSprite();
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Horizontal;
            img.fillOrigin = fromLeft
                ? (int)Image.OriginHorizontal.Left
                : (int)Image.OriginHorizontal.Right;
            img.color = color;
        }
    }
}
