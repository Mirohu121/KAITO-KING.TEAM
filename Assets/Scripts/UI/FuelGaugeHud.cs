using UnityEngine;
using UnityEngine.UI;

namespace Robogee.Player
{
    /// <summary>
    /// Binds motor fuel values to a Canvas Prefab (no code-built layout).
    /// Child names: BoostFill, JetFill, BoostStatus, JetStatus
    /// </summary>
    public class FuelGaugeHud : MonoBehaviour
    {
        /// <summary>False during unit select so fuel UI stays hidden.</summary>
        public static bool VisibleAllowed { get; set; }

        [SerializeField] UnitBMotor motor;
        [SerializeField] GameObject hudPrefab;
        [SerializeField] bool instantiateIfMissing = true;

        [Header("Wired views (auto-bound from prefab if empty)")]
        [SerializeField] Image boostFillImage;
        [SerializeField] Image jetFillImage;
        [SerializeField] Text boostStatus;
        [SerializeField] Text jetStatus;

        [Header("Active tint")]
        [SerializeField] Color boostFill = new Color(0.95f, 0.55f, 0.12f, 0.95f);
        [SerializeField] Color jetFill = new Color(0.25f, 0.78f, 0.92f, 0.95f);
        [SerializeField] Color labelColor = new Color(0.92f, 0.93f, 0.95f, 0.95f);
        [SerializeField] Color activeColor = new Color(1f, 0.85f, 0.35f, 1f);

        GameObject _instance;
        static Sprite _whiteSprite;

        void Awake()
        {
            if (motor == null)
                motor = GetComponentInParent<UnitBMotor>();
            if (motor == null)
                motor = GetComponent<UnitBMotor>();
        }

        void OnEnable()
        {
            if (!VisibleAllowed)
            {
                HideCanvas();
                return;
            }

            EnsureInstance();
            TryAutoWire();
            PrepareFill(boostFillImage, boostFill);
            PrepareFill(jetFillImage, jetFill);
            ApplyReadableStyle();
            if (_instance != null)
                _instance.SetActive(true);
        }

        void OnDisable()
        {
            // Keep shared canvas if another unit owns it; match start uses one player.
        }

        void LateUpdate()
        {
            if (!VisibleAllowed)
            {
                HideCanvas();
                return;
            }

            if (motor == null || boostFillImage == null || jetFillImage == null)
                return;

            boostFillImage.fillAmount = motor.JumpBoostFuelNormalized;
            jetFillImage.fillAmount = motor.DashJetFuelNormalized;

            bool boostActive = motor.IsJumpBoostActive;
            bool jetActive = motor.IsDashJetActive;

            if (boostStatus != null)
            {
                boostStatus.text = boostActive ? "ON" : "";
                boostStatus.color = boostActive ? activeColor : labelColor;
            }

            if (jetStatus != null)
            {
                jetStatus.text = jetActive ? "ON" : "";
                jetStatus.color = jetActive ? activeColor : labelColor;
            }

            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 8f);
            boostFillImage.color = boostActive ? boostFill * pulse : boostFill;
            jetFillImage.color = jetActive ? jetFill * pulse : jetFill;
        }

        public static void HideAll()
        {
            VisibleAllowed = false;
            var canvas = GameObject.Find("FuelHudCanvas");
            if (canvas != null)
                Object.Destroy(canvas);
        }

        public static void ShowForCombat()
        {
            VisibleAllowed = true;
        }

        void HideCanvas()
        {
            if (_instance != null)
            {
                _instance.SetActive(false);
                return;
            }

            var existing = GameObject.Find("FuelHudCanvas");
            if (existing != null)
                existing.SetActive(false);
        }

        void ApplyReadableStyle()
        {
            Transform root = _instance != null ? _instance.transform : transform;
            var fuelRoot = FindDeep<RectTransform>(root, "FuelRoot");
            if (fuelRoot != null)
            {
                // Wider so labels are not clipped.
                fuelRoot.anchorMin = new Vector2(0.01f, 0.26f);
                fuelRoot.anchorMax = new Vector2(0.22f, 0.74f);
                fuelRoot.offsetMin = Vector2.zero;
                fuelRoot.offsetMax = Vector2.zero;
            }

            WidenHolder(FindDeep<RectTransform>(root, "Boost"));
            WidenHolder(FindDeep<RectTransform>(root, "Jet"));

            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                if (text == null)
                    continue;

                text.horizontalOverflow = HorizontalWrapMode.Overflow;
                text.verticalOverflow = VerticalWrapMode.Overflow;
                text.resizeTextForBestFit = false;

                switch (text.name)
                {
                    case "Title":
                        text.text = "FUEL";
                        text.fontSize = 40;
                        break;
                    case "TopLabel":
                        // Short English — Japanese was clipping in the narrow bar column.
                        text.fontSize = 28;
                        if (text.transform.parent != null && text.transform.parent.name == "Boost")
                            text.text = "BOOST";
                        else if (text.transform.parent != null && text.transform.parent.name == "Jet")
                            text.text = "JET";
                        StretchLabel(text.rectTransform, new Vector2(-0.35f, 0.88f), new Vector2(1.35f, 1.02f));
                        break;
                    case "BottomLabel":
                        text.fontSize = 24;
                        if (text.transform.parent != null && text.transform.parent.name == "Boost")
                            text.text = "JUMP";
                        else if (text.transform.parent != null && text.transform.parent.name == "Jet")
                            text.text = "DASH";
                        StretchLabel(text.rectTransform, new Vector2(-0.35f, -0.02f), new Vector2(1.35f, 0.12f));
                        break;
                    case "BoostStatus":
                    case "Status":
                    case "JetStatus":
                        text.fontSize = 22;
                        break;
                }
            }
        }

        static void WidenHolder(RectTransform holder)
        {
            if (holder == null)
                return;
            holder.sizeDelta = new Vector2(96f, holder.sizeDelta.y);
        }

        static void StretchLabel(RectTransform rt, Vector2 amin, Vector2 amax)
        {
            if (rt == null)
                return;
            rt.anchorMin = amin;
            rt.anchorMax = amax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        void EnsureInstance()
        {
            if (!VisibleAllowed)
                return;
            if (boostFillImage != null && jetFillImage != null)
                return;
            if (!instantiateIfMissing)
                return;

            if (hudPrefab == null)
                hudPrefab = Resources.Load<GameObject>("UI/FuelHud");

            if (hudPrefab == null)
            {
                Debug.LogWarning("[FuelGaugeHud] FuelHud prefab missing.", this);
                return;
            }

            var existing = GameObject.Find("FuelHudCanvas");
            if (existing != null)
            {
                _instance = existing;
                _instance.SetActive(true);
                return;
            }

            _instance = Instantiate(hudPrefab);
            _instance.name = "FuelHudCanvas";
        }

        void TryAutoWire()
        {
            Transform root = _instance != null ? _instance.transform : transform;
            if (boostFillImage == null)
                boostFillImage = FindDeep<Image>(root, "BoostFill");
            if (jetFillImage == null)
                jetFillImage = FindDeep<Image>(root, "JetFill");
            if (boostStatus == null)
                boostStatus = FindDeep<Text>(root, "BoostStatus");
            if (boostStatus == null)
                boostStatus = FindDeep<Text>(root, "Status");
            if (jetStatus == null)
            {
                // Prefer Jet child's Status after rename, else JetStatus
                var jet = root.Find("FuelRoot/Bars/Jet") ?? FindDeep<Transform>(root, "Jet");
                if (jet != null)
                    jetStatus = FindDeep<Text>(jet, "JetStatus") ?? FindDeep<Text>(jet, "Status");
                if (jetStatus == null)
                    jetStatus = FindDeep<Text>(root, "JetStatus");
            }
        }

        static void PrepareFill(Image img, Color color)
        {
            if (img == null)
                return;
            if (img.sprite == null)
                img.sprite = WhiteSprite();
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Vertical;
            img.fillOrigin = (int)Image.OriginVertical.Bottom;
            img.color = color;
        }

        public static Sprite WhiteSprite()
        {
            if (_whiteSprite != null)
                return _whiteSprite;
            var tex = Texture2D.whiteTexture;
            _whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            return _whiteSprite;
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
    }
}
