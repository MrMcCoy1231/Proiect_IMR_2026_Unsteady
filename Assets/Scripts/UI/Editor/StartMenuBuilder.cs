using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using B = Unsteady.UI.EditorTools.TremorPanelBuilder;

namespace Unsteady.UI.EditorTools
{
    /// <summary>
    /// Construiește meniul de start, îl salvează ca prefab în Assets/Prefabs/UI/
    /// și îl pune în fața jucătorului. Leagă automat PanouTremor din scenă.
    /// </summary>
    static class StartMenuBuilder
    {
        const string PrefabPath = "Assets/Prefabs/UI/MeniuStart.prefab";
        const float Width = 700f, Height = 480f;

        [MenuItem("Unsteady/3. Creează meniul de start (UI)")]
        static void Build()
        {
            if (File.Exists(PrefabPath) &&
                !EditorUtility.DisplayDialog("Unsteady", "MeniuStart.prefab există deja. Îl refac de la zero?", "Da", "Nu"))
                return;

            // Scoate din scenă un meniu vechi, dacă există.
            var old = Object.FindFirstObjectByType<StartMenu>(FindObjectsInactive.Include);
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            // ---- Canvas ----
            var root = new GameObject("MeniuStart", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            root.AddComponent<TrackedDeviceGraphicRaycaster>();
            root.AddComponent<GraphicRaycaster>();
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(Width, Height);
            rt.localScale = Vector3.one * 0.001f;

            var bg = B.CreateImage("Fundal", rt, new Color(0.08f, 0.09f, 0.11f, 0.94f));
            B.Stretch(bg.rectTransform);

            // ---- Pagina principală ----
            var main = Page("Pagina_Principala", rt);
            B.CreateText("Titlu", main, "Unsteady", 72, FontStyles.Bold,
                new Vector2(0, 170), new Vector2(Width - 40, 90));
            B.CreateText("Subtitlu", main, "Simte cum e să trăiești cu tremor", 30, FontStyles.Normal,
                new Vector2(0, 105), new Vector2(Width - 40, 50)).color = new Color(1, 1, 1, 0.8f);

            var empathy = B.CreateButton("Buton_Empatie", main, "Empatie",
                new Vector2(0, 10), new Vector2(460, 100), out var eLabel);
            eLabel.fontSize = 38;
            ((Image)empathy.targetGraphic).color = new Color(0.95f, 0.55f, 0.20f);

            var test = B.CreateButton("Buton_Test", main, "Testează-ți mâna",
                new Vector2(0, -110), new Vector2(460, 100), out var tLabel);
            tLabel.fontSize = 38;

            B.CreateText("Nota", main, "Apasă cu raza (trigger) sau cu degetul", 22, FontStyles.Italic,
                new Vector2(0, -205), new Vector2(Width - 40, 40)).color = new Color(1, 1, 1, 0.55f);

            // ---- Pagina "în curând" ----
            var soon = Page("Pagina_InCurand", rt);
            B.CreateText("Titlu", soon, "Testează-ți mâna", 52, FontStyles.Bold,
                new Vector2(0, 160), new Vector2(Width - 40, 70));
            B.CreateText("Mesaj", soon,
                "Mini-jocul care măsoară tremorul mâinii tale vine în curând.\n\n" +
                "<size=80%><i>Rezultatul va fi orientativ, nu un diagnostic medical.</i></size>",
                30, FontStyles.Normal, new Vector2(0, 30), new Vector2(Width - 80, 180));
            var back = B.CreateButton("Buton_Inapoi", soon, "Înapoi",
                new Vector2(0, -150), new Vector2(300, 90), out _);
            soon.gameObject.SetActive(false);

            // ---- Script ----
            var menu = root.AddComponent<StartMenu>();
            menu.mainPage = main.gameObject;
            menu.comingSoonPage = soon.gameObject;
            menu.empathyButton = empathy;
            menu.testButton = test;
            menu.backButton = back;

            Place(root.transform);
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(root, PrefabPath, InteractionMode.UserAction);

            // Legătura cu PanouTremor e din scenă, deci o punem pe instanță, nu în prefab.
            var panel = Object.FindFirstObjectByType<TremorPanel>(FindObjectsInactive.Include);
            if (panel != null)
            {
                menu.showOnEmpathy = new[] { panel.gameObject };
                PrefabUtility.RecordPrefabInstancePropertyModifications(menu);
            }
            else
                Debug.LogWarning("[MeniuStart] Nu am găsit PanouTremor în scenă. Rulează întâi Unsteady → 2.");

            Undo.RegisterCreatedObjectUndo(root, "Creează MeniuStart");
            EditorSceneManager.MarkSceneDirty(root.scene);
            Selection.activeObject = root;
            EditorGUIUtility.PingObject(prefab);
            Debug.Log("[MeniuStart] Meniu creat: " + PrefabPath + ". Salvează scena (Ctrl+S).");
        }

        static RectTransform Page(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            B.Stretch(r);
            return r;
        }

        // În fața camerei, la 1.5 m, la înălțimea ochilor.
        static void Place(Transform t)
        {
            var cam = Camera.main != null ? Camera.main.transform : null;
            Vector3 eye = cam != null ? cam.position : Vector3.zero;
            Vector3 fwd = cam != null ? cam.forward : Vector3.forward;
            fwd.y = 0; fwd = fwd.sqrMagnitude > 0.001f ? fwd.normalized : Vector3.forward;

            Vector3 pos = new Vector3(eye.x, 0, eye.z) + fwd * 1.5f;
            pos.y = eye.y > 1f ? eye.y : 1.45f;
            t.position = pos;
            t.rotation = Quaternion.LookRotation(fwd);
        }
    }
}
