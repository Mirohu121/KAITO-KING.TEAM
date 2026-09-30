#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Robogee.EditorTools
{
    /// <summary>
    /// Ensures Canvas UI prefabs exist for artists. No menu items — runs on editor load.
    /// </summary>
    [InitializeOnLoad]
    public static class UiPrefabAutoEnsure
    {
        const string FuelPath = "Assets/UI/Prefabs/FuelHud.prefab";
        const string MatchPath = "Assets/UI/Prefabs/MatchHud.prefab";
        const string SelectPath = "Assets/UI/Prefabs/UnitSelectHud.prefab";
        const string FuelResourcePath = "Assets/Resources/UI/FuelHud.prefab";
        const string MatchResourcePath = "Assets/Resources/UI/MatchHud.prefab";
        const string SelectResourcePath = "Assets/Resources/UI/UnitSelectHud.prefab";

        static UiPrefabAutoEnsure()
        {
            EditorApplication.delayCall += Ensure;
        }

        static void Ensure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            EnsureDir("Assets/UI");
            EnsureDir("Assets/UI/Prefabs");
            EnsureDir("Assets/Resources");
            EnsureDir("Assets/Resources/UI");
            EnsureDir("Assets/Resources/RobotPrefab");

            // Rebuild HUDs when readability pass changes.
            if (!EditorPrefs.GetBool("Robogee.HudReadable.v4", false))
            {
                AssetDatabase.DeleteAsset(SelectPath);
                AssetDatabase.DeleteAsset(SelectResourcePath);
                AssetDatabase.DeleteAsset(FuelPath);
                AssetDatabase.DeleteAsset(FuelResourcePath);
                AssetDatabase.DeleteAsset(MatchPath);
                AssetDatabase.DeleteAsset(MatchResourcePath);
                EditorPrefs.SetBool("Robogee.HudReadable.v4", true);
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(FuelPath) == null)
            {
                var fuel = BuildFuelHud();
                SavePrefab(fuel, FuelPath);
                Object.DestroyImmediate(fuel);
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(MatchPath) == null)
            {
                var match = BuildMatchHud();
                SavePrefab(match, MatchPath);
                Object.DestroyImmediate(match);
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(SelectPath) == null)
            {
                var select = BuildUnitSelectHud();
                SavePrefab(select, SelectPath);
                Object.DestroyImmediate(select);
            }

            CopyPrefab(FuelPath, FuelResourcePath);
            CopyPrefab(MatchPath, MatchResourcePath);
            CopyPrefab(SelectPath, SelectResourcePath);

            // Runtime-loadable copies of A/B/C (stats untouched — file copy only).
            CopyPrefab("Assets/RobotPrefab/Player_UnitA_Cube.prefab", "Assets/Resources/RobotPrefab/UnitA.prefab");
            CopyPrefab("Assets/RobotPrefab/Player_UnitB_Cube.prefab", "Assets/Resources/RobotPrefab/UnitB.prefab");
            CopyPrefab("Assets/RobotPrefab/Player_UnitC_Cube.prefab", "Assets/Resources/RobotPrefab/UnitC.prefab");
        }

        static void EnsureDir(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
                EnsureDir(parent);
            AssetDatabase.CreateFolder(parent ?? "Assets", name);
        }

        static void CopyPrefab(string src, string dst)
        {
            var srcGo = AssetDatabase.LoadAssetAtPath<GameObject>(src);
            if (srcGo == null)
                return;
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(dst);
            if (existing != null)
                return;
            AssetDatabase.CopyAsset(src, dst);
        }

        static void SavePrefab(GameObject go, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(go, path);
            AssetDatabase.SaveAssets();
        }

        static Font UiFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font;
        }

        static GameObject BuildFuelHud()
        {
            var root = CreateCanvas("FuelHudCanvas", 50);
            var panel = CreateRect("FuelRoot", root.transform);
            Stretch(panel.GetComponent<RectTransform>(), new Vector2(0.015f, 0.28f), new Vector2(0.16f, 0.72f));

            CreateLabel(panel.transform, "Title", "FUEL", 36, TextAnchor.MiddleLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 4f), new Vector2(0f, 34f));

            var boost = CreateVerticalBar(panel.transform, "Boost", "ブースト", "JUMP",
                new Color(0.95f, 0.55f, 0.12f), new Vector2(0.22f, 0f), new Vector2(0.22f, 1f));
            boost.name = "Boost";
            var jet = CreateVerticalBar(panel.transform, "Jet", "ジェット", "DASH",
                new Color(0.25f, 0.78f, 0.92f), new Vector2(0.72f, 0f), new Vector2(0.72f, 1f));
            jet.name = "Jet";

            // Rename fill/status for binder lookup.
            RenameDeep(boost, "Fill", "BoostFill");
            RenameDeep(boost, "Status", "BoostStatus");
            RenameDeep(jet, "Fill", "JetFill");
            RenameDeep(jet, "Status", "JetStatus");

            var cg = root.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            return root;
        }

        static GameObject BuildMatchHud()
        {
            var root = CreateCanvas("MatchHudCanvas", 60);

            // Player HP — keep below top edge so labels aren't clipped.
            var pTrack = CreateImage(root.transform, "PlayerHpTrack", new Color(0.1f, 0.1f, 0.12f, 0.7f));
            Stretch(pTrack.rectTransform, new Vector2(0.02f, 0.845f), new Vector2(0.30f, 0.895f));
            var pFill = CreateImage(root.transform, "PlayerHpFill", new Color(0.3f, 0.85f, 0.4f, 0.95f));
            Stretch(pFill.rectTransform, new Vector2(0.02f, 0.845f), new Vector2(0.30f, 0.895f));
            pFill.type = Image.Type.Filled;
            pFill.fillMethod = Image.FillMethod.Horizontal;
            pFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            pFill.fillAmount = 1f;
            CreateLabel(root.transform, "PlayerHpLabel", "自機", 32, TextAnchor.MiddleLeft,
                new Vector2(0.02f, 0.905f), new Vector2(0.30f, 0.955f), Vector2.zero, Vector2.zero);

            var eTrack = CreateImage(root.transform, "EnemyHpTrack", new Color(0.1f, 0.1f, 0.12f, 0.7f));
            Stretch(eTrack.rectTransform, new Vector2(0.70f, 0.845f), new Vector2(0.98f, 0.895f));
            var eFill = CreateImage(root.transform, "EnemyHpFill", new Color(0.95f, 0.35f, 0.3f, 0.95f));
            Stretch(eFill.rectTransform, new Vector2(0.70f, 0.845f), new Vector2(0.98f, 0.895f));
            eFill.type = Image.Type.Filled;
            eFill.fillMethod = Image.FillMethod.Horizontal;
            eFill.fillOrigin = (int)Image.OriginHorizontal.Right;
            eFill.fillAmount = 1f;
            CreateLabel(root.transform, "EnemyHpLabel", "敵", 32, TextAnchor.MiddleRight,
                new Vector2(0.70f, 0.905f), new Vector2(0.98f, 0.955f), Vector2.zero, Vector2.zero);

            CreateLabel(root.transform, "TimerText", "03:00", 64, TextAnchor.MiddleCenter,
                new Vector2(0.35f, 0.86f), new Vector2(0.65f, 0.96f), Vector2.zero, Vector2.zero);

            var result = CreateLabel(root.transform, "ResultText", "", 56, TextAnchor.MiddleCenter,
                new Vector2(0.15f, 0.42f), new Vector2(0.85f, 0.62f), Vector2.zero, Vector2.zero);
            result.gameObject.SetActive(false);

            var cg = root.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;
            return root;
        }

        static GameObject BuildUnitSelectHud()
        {
            var root = CreateCanvas("UnitSelectHudCanvas", 80);
            var dim = CreateImage(root.transform, "Dim", new Color(0f, 0f, 0f, 0.35f));
            dim.raycastTarget = true;
            Stretch(dim.rectTransform, Vector2.zero, Vector2.one);

            CreateLabel(root.transform, "TitleText", "機体を選択", 72, TextAnchor.MiddleCenter,
                new Vector2(0.1f, 0.74f), new Vector2(0.9f, 0.92f), Vector2.zero, Vector2.zero);

            CreateUnitButton(root.transform, "ButtonA", "A",
                new Color(0.25f, 0.7f, 1f, 0.95f), new Vector2(0.08f, 0.28f), new Vector2(0.34f, 0.66f));
            CreateUnitButton(root.transform, "ButtonB", "B",
                new Color(0.45f, 0.85f, 0.4f, 0.95f), new Vector2(0.37f, 0.28f), new Vector2(0.63f, 0.66f));
            CreateUnitButton(root.transform, "ButtonC", "C",
                new Color(0.95f, 0.55f, 0.25f, 0.95f), new Vector2(0.66f, 0.28f), new Vector2(0.92f, 0.66f));

            CreateLabel(root.transform, "HintText", "選んで 1v1 開始", 42, TextAnchor.MiddleCenter,
                new Vector2(0.15f, 0.10f), new Vector2(0.85f, 0.24f), Vector2.zero, Vector2.zero);

            return root;
        }

        static void CreateUnitButton(Transform parent, string name, string label, Color color, Vector2 amin, Vector2 amax)
        {
            var go = CreateRect(name, parent);
            Stretch(go.GetComponent<RectTransform>(), amin, amax);
            var img = go.AddComponent<Image>();
            img.sprite = WhiteSprite();
            img.color = color;
            img.raycastTarget = true;
            var button = go.AddComponent<Button>();
            button.targetGraphic = img;

            CreateLabel(go.transform, "Label", label, 96, TextAnchor.MiddleCenter,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        static GameObject CreateVerticalBar(Transform parent, string id, string top, string bottom, Color fillColor, Vector2 amin, Vector2 amax)
        {
            var holder = CreateRect(id, parent);
            var rt = holder.GetComponent<RectTransform>();
            rt.anchorMin = amin;
            rt.anchorMax = amax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(56f, 0f);
            rt.anchoredPosition = Vector2.zero;

            CreateLabel(holder.transform, "TopLabel", top, 24, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.9f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            CreateLabel(holder.transform, "Status", "", 20, TextAnchor.MiddleCenter,
                new Vector2(0f, 0.82f), new Vector2(1f, 0.9f), Vector2.zero, Vector2.zero);

            var track = CreateImage(holder.transform, "Track", new Color(0.08f, 0.09f, 0.11f, 0.75f));
            Stretch(track.rectTransform, new Vector2(0.35f, 0.12f), new Vector2(0.65f, 0.8f));

            var fill = CreateImage(holder.transform, "Fill", fillColor);
            Stretch(fill.rectTransform, new Vector2(0.35f, 0.12f), new Vector2(0.65f, 0.8f));
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Vertical;
            fill.fillOrigin = (int)Image.OriginVertical.Bottom;
            fill.fillAmount = 1f;

            CreateLabel(holder.transform, "BottomLabel", bottom, 20, TextAnchor.MiddleCenter,
                new Vector2(0f, 0f), new Vector2(1f, 0.1f), Vector2.zero, Vector2.zero);
            return holder;
        }

        static GameObject CreateCanvas(string name, int sort)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sort;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return go;
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

        static Sprite WhiteSprite()
        {
            var tex = Texture2D.whiteTexture;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        }

        static Text CreateLabel(
            Transform parent, string name, string content, int size, TextAnchor align,
            Vector2 amin, Vector2 amax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = CreateRect(name, parent);
            var text = go.AddComponent<Text>();
            text.font = UiFont();
            text.text = content;
            text.fontSize = size;
            text.alignment = align;
            text.color = Color.white;
            text.raycastTarget = false;
            Stretch(text.rectTransform, amin, amax, offsetMin, offsetMax);
            return text;
        }

        static void Stretch(RectTransform rt, Vector2 amin, Vector2 amax)
        {
            Stretch(rt, amin, amax, Vector2.zero, Vector2.zero);
        }

        static void Stretch(RectTransform rt, Vector2 amin, Vector2 amax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = amin;
            rt.anchorMax = amax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }

        static void RenameDeep(GameObject root, string from, string to)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == from)
                    t.name = to;
            }
        }
    }
}
#endif
