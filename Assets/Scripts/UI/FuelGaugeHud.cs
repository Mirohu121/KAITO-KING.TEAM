using UnityEngine;
using UnityEngine.UI;

namespace Robogee.Player
{
    /// <summary>
    /// Left-middle dual vertical fuel gauges (Valkyrie-inspired, custom colors).
    /// Builds its own Canvas at runtime so TestField stays simple.
    /// </summary>
    public class FuelGaugeHud : MonoBehaviour
    {
        [SerializeField] UnitBMotor motor;
        [SerializeField] bool buildOnAwake = true;

        [Header("Layout (screen %)")]
        [SerializeField] Vector2 anchorMin = new Vector2(0.02f, 0.32f);
        [SerializeField] Vector2 anchorMax = new Vector2(0.10f, 0.68f);

        [Header("Colors (not Valkyrie green)")]
        [SerializeField] Color boostFill = new Color(0.95f, 0.55f, 0.12f, 0.95f);
        [SerializeField] Color jetFill = new Color(0.25f, 0.78f, 0.92f, 0.95f);
        [SerializeField] Color trackColor = new Color(0.08f, 0.09f, 0.11f, 0.72f);
        [SerializeField] Color frameColor = new Color(0.75f, 0.78f, 0.82f, 0.55f);
        [SerializeField] Color labelColor = new Color(0.92f, 0.93f, 0.95f, 0.95f);
        [SerializeField] Color activeColor = new Color(1f, 0.85f, 0.35f, 1f);

        [Header("Bar look")]
        [SerializeField] float barWidth = 18f;
        [SerializeField] int tickCount = 8;

        Image _boostFillImage;
        Image _jetFillImage;
        Text _boostStatus;
        Text _jetStatus;
        CanvasGroup _group;

        void Awake()
        {
            if (motor == null)
                motor = GetComponent<UnitBMotor>();
            if (motor == null)
                motor = FindFirstObjectByType<UnitBMotor>();

            if (buildOnAwake)
                BuildUi();
        }

        void LateUpdate()
        {
            if (motor == null || _boostFillImage == null)
                return;

            _boostFillImage.fillAmount = motor.JumpBoostFuelNormalized;
            _jetFillImage.fillAmount = motor.DashJetFuelNormalized;

            bool boostActive = motor.IsJumpBoostActive;
            bool jetActive = motor.IsDashJetActive;

            _boostStatus.text = boostActive ? "[使用中]" : "";
            _boostStatus.color = boostActive ? activeColor : labelColor;
            _jetStatus.text = jetActive ? "[使用中]" : "";
            _jetStatus.color = jetActive ? activeColor : labelColor;

            // Soft pulse while draining
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 8f);
            _boostFillImage.color = boostActive ? boostFill * pulse : boostFill;
            _jetFillImage.color = jetActive ? jetFill * pulse : jetFill;
        }

        public void BuildUi()
        {
            // Tear down previous auto HUD if re-run
            var existing = transform.Find("FuelGaugeCanvas");
            if (existing != null)
                Destroy(existing.gameObject);

            var canvasGo = new GameObject("FuelGaugeCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            _group = canvasGo.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            var root = CreateRect("FuelRoot", canvasGo.transform);
            var rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = anchorMin;
            rootRt.anchorMax = anchorMax;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var title = CreateLabel(root.transform, "Title", "FUEL", 18, TextAnchor.LowerLeft);
            var titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 0f);
            titleRt.anchoredPosition = new Vector2(0f, 6f);
            titleRt.sizeDelta = new Vector2(0f, 24f);

            var row = CreateRect("Bars", root.transform);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 0.08f);
            rowRt.anchorMax = new Vector2(1f, 0.92f);
            rowRt.offsetMin = Vector2.zero;
            rowRt.offsetMax = Vector2.zero;

            _boostFillImage = CreateVerticalGauge(
                row.transform,
                "Boost",
                "ブースト",
                "ジャンプ",
                boostFill,
                new Vector2(0.15f, 0.5f),
                out _boostStatus);

            _jetFillImage = CreateVerticalGauge(
                row.transform,
                "Jet",
                "ジェット",
                "ダッシュ",
                jetFill,
                new Vector2(0.70f, 0.5f),
                out _jetStatus);
        }

        Image CreateVerticalGauge(
            Transform parent,
            string id,
            string topLabel,
            string bottomLabel,
            Color fillColor,
            Vector2 anchorCenter,
            out Text statusLabel)
        {
            var holder = CreateRect(id, parent);
            var holderRt = holder.GetComponent<RectTransform>();
            holderRt.anchorMin = anchorCenter;
            holderRt.anchorMax = anchorCenter;
            holderRt.pivot = new Vector2(0.5f, 0.5f);
            holderRt.sizeDelta = new Vector2(barWidth + 36f, 0f);
            holderRt.offsetMin = new Vector2(holderRt.offsetMin.x, 0f);
            holderRt.offsetMax = new Vector2(holderRt.offsetMax.x, 0f);
            // Stretch height with parent
            holderRt.anchorMin = new Vector2(anchorCenter.x, 0f);
            holderRt.anchorMax = new Vector2(anchorCenter.x, 1f);
            holderRt.anchoredPosition = Vector2.zero;
            holderRt.sizeDelta = new Vector2(barWidth + 40f, 0f);

            var top = CreateLabel(holder.transform, "TopLabel", topLabel, 14, TextAnchor.MiddleCenter);
            Stretch(top.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 2f), new Vector2(0f, 22f));

            statusLabel = CreateLabel(holder.transform, "Status", "", 12, TextAnchor.MiddleCenter);
            Stretch(statusLabel.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -18f), new Vector2(0f, 2f));
            statusLabel.color = activeColor;

            var frame = CreateImage(holder.transform, "Frame", frameColor);
            Stretch(frame.rectTransform, new Vector2(0.5f, 0.06f), new Vector2(0.5f, 0.82f),
                new Vector2(-(barWidth * 0.5f + 3f), 0f), new Vector2(barWidth * 0.5f + 3f, 0f));

            var track = CreateImage(holder.transform, "Track", trackColor);
            Stretch(track.rectTransform, new Vector2(0.5f, 0.07f), new Vector2(0.5f, 0.81f),
                new Vector2(-barWidth * 0.5f, 0f), new Vector2(barWidth * 0.5f, 0f));

            var fill = CreateImage(holder.transform, "Fill", fillColor);
            Stretch(fill.rectTransform, new Vector2(0.5f, 0.07f), new Vector2(0.5f, 0.81f),
                new Vector2(-barWidth * 0.5f, 0f), new Vector2(barWidth * 0.5f, 0f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            fill.fillAmount = 1f;

            // Side ticks (left of bar) — different from Valkyrie's right-side HUD
            var ticks = CreateRect("Ticks", holder.transform);
            Stretch(ticks.GetComponent<RectTransform>(), new Vector2(0.5f, 0.07f), new Vector2(0.5f, 0.81f),
                new Vector2(-(barWidth * 0.5f + 10f), 0f), new Vector2(-(barWidth * 0.5f + 2f), 0f));
            for (int i = 0; i <= tickCount; i++)
            {
                float t = i / (float)tickCount;
                var tick = CreateImage(ticks.transform, $"Tick_{i}", new Color(1f, 1f, 1f, 0.35f));
                var tr = tick.rectTransform;
                tr.anchorMin = new Vector2(0f, t);
                tr.anchorMax = new Vector2(1f, t);
                tr.pivot = new Vector2(0.5f, 0.5f);
                tr.sizeDelta = new Vector2(0f, i % 2 == 0 ? 2.5f : 1.5f);
                tr.anchoredPosition = Vector2.zero;
            }

            var bottom = CreateLabel(holder.transform, "BottomLabel", bottomLabel, 12, TextAnchor.MiddleCenter);
            Stretch(bottom.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 0f), new Vector2(0f, 22f));

            return fill;
        }

        static GameObject CreateRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = CreateRect(name, parent);
            var img = go.AddComponent<Image>();
            img.sprite = WhiteSprite();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static Text CreateLabel(Transform parent, string name, string content, int size, TextAnchor align)
        {
            var go = CreateRect(name, parent);
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (text.font == null)
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (text.font == null)
                text.font = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic UI", "Meiryo", "Segoe UI", "Arial" }, size);
            text.text = content;
            text.fontSize = size;
            text.alignment = align;
            text.color = new Color(0.92f, 0.93f, 0.95f, 0.95f);
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        static void Stretch(RectTransform rt, Vector2 amin, Vector2 amax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = amin;
            rt.anchorMax = amax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        static Sprite _whiteSprite;
        static Sprite WhiteSprite()
        {
            if (_whiteSprite != null)
                return _whiteSprite;
            var tex = Texture2D.whiteTexture;
            _whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            return _whiteSprite;
        }
    }
}
