using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Unsteady.UI.EditorTools
{
    /// <summary>
    /// Construiește automat panoul de control (World Space Canvas), îl salvează ca
    /// prefab în Assets/Prefabs/UI/ și îl pune în scena deschisă, lângă masă.
    /// </summary>
    static class TremorPanelBuilder
    {
        const string PrefabFolder = "Assets/Prefabs/UI";
        const string PrefabPath = PrefabFolder + "/PanouTremor.prefab";

        // Dimensiuni în "pixeli" de canvas. Scara 0.001 => 1 pixel = 1 mm.
        const float Width = 620f, Height = 440f;

        [MenuItem("Unsteady/2. Creează panoul de control (UI)")]
        static void Build()
        {
            if (File.Exists(PrefabPath) &&
                !EditorUtility.DisplayDialog("Unsteady", "PanouTremor.prefab există deja. Îl refac de la zero?", "Da", "Nu"))
                return;

            // ---- Canvas ----
            var root = new GameObject("PanouTremor", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            root.AddComponent<TrackedDeviceGraphicRaycaster>(); // raza / degetul din VR
            root.AddComponent<GraphicRaycaster>();              // mouse-ul în Editor

            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(Width, Height);
            rt.localScale = Vector3.one * 0.001f;

            // ---- Fundal ----
            var bg = CreateImage("Fundal", rt, new Color(0.08f, 0.09f, 0.11f, 0.92f));
            Stretch(bg.rectTransform);

            // ---- Texte ----
            CreateText("Titlu", rt, "Simulator de tremor", 40, FontStyles.Bold,
                new Vector2(0, 175), new Vector2(Width - 40, 60));
            var status = CreateText("Status", rt, "Tremor: Oprit", 32, FontStyles.Normal,
                new Vector2(0, 115), new Vector2(Width - 40, 50));

            // ---- Butoane nivel ----
            string[] levels = { "Oprit", "Ușor", "Moderat", "Sever" };
            var levelButtons = new Button[4];
            float bw = 132f, gap = 12f;
            float startX = -(4 * bw + 3 * gap) / 2f + bw / 2f;
            for (int i = 0; i < 4; i++)
                levelButtons[i] = CreateButton("Nivel_" + levels[i], rt, levels[i],
                    new Vector2(startX + i * (bw + gap), 30), new Vector2(bw, 90), out _);

            // ---- Profil + vibrație ----
            var profileBtn = CreateButton("Buton_Profil", rt, "Parkinson (~5 Hz)",
                new Vector2(-146, -95), new Vector2(276, 90), out var profileLabel);
            var hapticBtn = CreateButton("Buton_Vibratie", rt, "Vibrație: PORNITĂ",
                new Vector2(146, -95), new Vector2(276, 90), out var hapticLabel);

            CreateText("Nota", rt, "Apasă cu raza (trigger) sau cu degetul", 22, FontStyles.Italic,
                new Vector2(0, -180), new Vector2(Width - 40, 40)).color = new Color(1, 1, 1, 0.55f);

            // ---- Script ----
            var panel = root.AddComponent<TremorPanel>();
            panel.levelButtons = levelButtons;
            panel.profileButton = profileBtn;
            panel.hapticsButton = hapticBtn;
            panel.statusText = status;
            panel.profileLabel = profileLabel;
            panel.hapticsLabel = hapticLabel;

            // ---- Salvare prefab + plasare în scenă ----
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/Prefabs", "UI");

            Place(root.transform);
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.UserAction);
            Undo.RegisterCreatedObjectUndo(root, "Creează PanouTremor");
            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeObject = root;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("[TremorPanel] Panou creat: " + PrefabPath + ". Mută-l unde vrei și salvează scena (Ctrl+S).");
        }

        // Pune panoul deasupra mesei, întors spre jucător.
        static void Place(Transform panel)
        {
            Vector3 player = Camera.main != null ? Camera.main.transform.position : Vector3.up * 1.6f;
            Vector3 pos;

            var masa = GameObject.Find("Masa");
            var r = masa != null ? masa.GetComponentInChildren<Renderer>() : null;
            if (r != null)
                pos = new Vector3(r.bounds.center.x, r.bounds.max.y + 0.45f, r.bounds.center.z);
            else
            {
                var fwd = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
                fwd.y = 0; fwd = fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;
                pos = new Vector3(player.x, 1.3f, player.z) + fwd * 1.2f;
            }

            // Fața vizibilă a unui canvas e spre -Z, deci "forward" trebuie să fugă de jucător.
            Vector3 away = pos - player; away.y = 0;
            panel.position = pos;
            panel.rotation = away.sqrMagnitude > 0.001f ? Quaternion.LookRotation(away.normalized) : Quaternion.identity;
        }

        // ---------- helpers ----------

        internal static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }

        internal static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            img.color = color;
            return img;
        }

        internal static TextMeshProUGUI CreateText(string name, Transform parent, string text, float size, FontStyles style,
            Vector2 pos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            r.anchoredPosition = pos; r.sizeDelta = sizeDelta;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = size; tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            return tmp;
        }

        internal static Button CreateButton(string name, Transform parent, string label, Vector2 pos, Vector2 size,
            out TextMeshProUGUI labelText)
        {
            var img = CreateImage(name, parent, new Color(0.20f, 0.23f, 0.28f));
            var r = img.rectTransform;
            r.anchoredPosition = pos; r.sizeDelta = size;

            var btn = img.gameObject.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.pressedColor = new Color(0.65f, 0.65f, 0.65f);
            btn.colors = colors;

            labelText = CreateText("Text", r, label, 28, FontStyles.Bold, Vector2.zero, size);
            Stretch(labelText.rectTransform);
            return btn;
        }
    }
}
