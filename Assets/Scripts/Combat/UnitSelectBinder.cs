using System;
using UnityEngine;
using UnityEngine.UI;
using Robogee.Player;

namespace Robogee.Combat
{
    public enum UnitId
    {
        A = 0,
        B = 1,
        C = 2
    }

    /// <summary>
    /// Shows Canvas Prefab unit select (A/B/C). Does not alter prefab stats.
    /// Child button names: ButtonA, ButtonB, ButtonC. Optional: TitleText.
    /// </summary>
    public class UnitSelectBinder : MonoBehaviour
    {
        [SerializeField] GameObject hudPrefab;
        [SerializeField] Button buttonA;
        [SerializeField] Button buttonB;
        [SerializeField] Button buttonC;

        GameObject _instance;
        Action<UnitId> _onPicked;
        bool _picked;

        public bool IsOpen => _instance != null && _instance.activeSelf;

        public void Show(Action<UnitId> onPicked)
        {
            _onPicked = onPicked;
            _picked = false;
            EnsureInstance();
            WireButtons();
            ApplyReadableStyle();
            if (_instance != null)
                _instance.SetActive(true);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 0f;
        }

        public void Hide()
        {
            if (_instance != null)
                _instance.SetActive(false);
            Time.timeScale = 1f;
        }

        void ApplyReadableStyle()
        {
            if (_instance == null)
                return;

            // Soften full-screen dim so it doesn't feel like an error overlay.
            var dim = _instance.transform.Find("Dim");
            if (dim != null)
            {
                var img = dim.GetComponent<Image>();
                if (img != null)
                    img.color = new Color(0f, 0f, 0f, 0.35f);
            }

            foreach (var text in _instance.GetComponentsInChildren<Text>(true))
            {
                if (text == null)
                    continue;
                if (text.name == "TitleText")
                {
                    text.fontSize = 72;
                    text.text = "機体を選択";
                    text.resizeTextForBestFit = false;
                }
                else if (text.name == "HintText")
                {
                    text.fontSize = 42;
                    text.text = "選んで 1v1 開始";
                    text.horizontalOverflow = HorizontalWrapMode.Overflow;
                    text.verticalOverflow = VerticalWrapMode.Overflow;
                    var rt = text.rectTransform;
                    rt.anchorMin = new Vector2(0.15f, 0.10f);
                    rt.anchorMax = new Vector2(0.85f, 0.24f);
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                }
                else if (text.name == "Label")
                {
                    // Button labels: A / B / C only
                    var parent = text.transform.parent;
                    if (parent != null)
                    {
                        if (parent.name == "ButtonA") text.text = "A";
                        else if (parent.name == "ButtonB") text.text = "B";
                        else if (parent.name == "ButtonC") text.text = "C";
                    }
                    text.fontSize = 96;
                    text.resizeTextForBestFit = false;
                    text.alignment = TextAnchor.MiddleCenter;
                }
                else
                {
                    text.fontSize = Mathf.Max(text.fontSize, 28);
                }
            }
        }

        void EnsureInstance()
        {
            if (_instance != null)
                return;

            if (hudPrefab == null)
                hudPrefab = Resources.Load<GameObject>("UI/UnitSelectHud");

            if (hudPrefab == null)
            {
                Debug.LogWarning("[UnitSelect] UnitSelectHud prefab missing.", this);
                return;
            }

            var existing = GameObject.Find("UnitSelectHudCanvas");
            if (existing != null)
            {
                _instance = existing;
                return;
            }

            _instance = Instantiate(hudPrefab);
            _instance.name = "UnitSelectHudCanvas";
        }

        void WireButtons()
        {
            if (_instance == null)
                return;

            if (buttonA == null)
                buttonA = FindButton("ButtonA");
            if (buttonB == null)
                buttonB = FindButton("ButtonB");
            if (buttonC == null)
                buttonC = FindButton("ButtonC");

            Bind(buttonA, UnitId.A);
            Bind(buttonB, UnitId.B);
            Bind(buttonC, UnitId.C);
        }

        void Bind(Button button, UnitId id)
        {
            if (button == null)
                return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => Pick(id));
        }

        void Pick(UnitId id)
        {
            if (_picked)
                return;
            _picked = true;
            Hide();
            _onPicked?.Invoke(id);
        }

        Button FindButton(string name)
        {
            var all = _instance.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == name)
                    return all[i];
            }
            return null;
        }
    }

    /// <summary>Loads A/B/C robot prefabs without modifying their tuned stats.</summary>
    public static class UnitPrefabCatalog
    {
        /// <summary>Temporary: spawn gaikotu skeleton instead of A/B/C cubes.</summary>
        public const bool UseGaikotuPlaceholder = true;

        public static GameObject Load(UnitId id)
        {
            if (UseGaikotuPlaceholder)
            {
                var gaikotu = Resources.Load<GameObject>("Gaikotu/gaikotu_playable");
                if (gaikotu != null)
                    return gaikotu;
#if UNITY_EDITOR
                var editorGaikotu = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Resources/Gaikotu/gaikotu_playable.prefab");
                if (editorGaikotu != null)
                    return editorGaikotu;
                var rig = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/gaikotu_rig.prefab");
                if (rig != null)
                    return rig;
#endif
            }

            string resourceName = id switch
            {
                UnitId.A => "RobotPrefab/UnitA",
                UnitId.C => "RobotPrefab/UnitC",
                _ => "RobotPrefab/UnitB"
            };

            var fromResources = Resources.Load<GameObject>(resourceName);
            if (fromResources != null)
                return fromResources;

#if UNITY_EDITOR
            string path = id switch
            {
                UnitId.A => "Assets/RobotPrefab/Player_UnitA_Cube.prefab",
                UnitId.C => "Assets/RobotPrefab/Player_UnitC_Cube.prefab",
                _ => "Assets/RobotPrefab/Player_UnitB_Cube.prefab"
            };
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
#else
            return null;
#endif
        }

        public static string DisplayName(UnitId id) => id switch
        {
            UnitId.A => "A",
            UnitId.C => "C",
            _ => "B"
        };
    }
}
