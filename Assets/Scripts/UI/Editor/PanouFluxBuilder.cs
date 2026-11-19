using System.IO;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Unsteady.Flux;
using B = Unsteady.UI.EditorTools.TremorPanelBuilder;

namespace Unsteady.UI.EditorTools
{
    /// <summary>
    /// Construiește panourile modului Empatie (PanouFlux), pune EmpatieFlow în SampleScene,
    /// îl leagă de meniul de start și de panoul de tremor și adaugă InaltimeJucator pe XR Origin.
    /// </summary>
    static class PanouFluxBuilder
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";
        const string PrefabPath = "Assets/Prefabs/UI/PanouFlux.prefab";
        const float W = 700f, H = 480f;

        static readonly Color Bg = new Color(0.08f, 0.09f, 0.11f, 0.94f);
        static readonly Color Gri = new Color(1, 1, 1, 0.6f);
        static readonly Color Turcoaz = new Color32(0x3A, 0xA6, 0xA0, 0xFF);
        static readonly Color Coral = new Color32(0xF2, 0x6D, 0x5B, 0xFF);
        static readonly Color Portocaliu = new Color(0.95f, 0.55f, 0.20f);

        [MenuItem("Unsteady/7. Creează fluxul Empatie (panouri + logică) în SampleScene")]
        static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            foreach (var old in Object.FindObjectsByType<PanouFlux>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Undo.DestroyObjectImmediate(old.gameObject);
            foreach (var old in Object.FindObjectsByType<EmpatieFlow>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Undo.DestroyObjectImmediate(old.gameObject);

            var panou = BuildPanel();

            // Așezat în locul meniului de start (în fața jucătorului).
            var meniu = Object.FindFirstObjectByType<StartMenu>(FindObjectsInactive.Include);
            if (meniu != null)
            {
                panou.transform.SetPositionAndRotation(meniu.transform.position, meniu.transform.rotation);
            }
            else if (Camera.main != null)
            {
                var c = Camera.main.transform;
                var f = c.forward; f.y = 0; f = f.sqrMagnitude > 0.001f ? f.normalized : Vector3.forward;
                panou.transform.position = new Vector3(c.position.x, 1.45f, c.position.z) + f * 1.5f;
                panou.transform.rotation = Quaternion.LookRotation(f);
            }

            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/UI")) AssetDatabase.CreateFolder("Assets/Prefabs", "UI");
            PrefabUtility.SaveAsPrefabAssetAndConnect(panou.gameObject, PrefabPath, InteractionMode.UserAction);
            Undo.RegisterCreatedObjectUndo(panou.gameObject, "PanouFlux");

            // Logica.
            var flowGo = new GameObject("Empatie Flow");
            Undo.RegisterCreatedObjectUndo(flowGo, "Empatie Flow");
            var flow = flowGo.AddComponent<EmpatieFlow>();
            flow.panou = panou;
            flow.meniu = meniu;
            var tp = Object.FindFirstObjectByType<TremorPanel>(FindObjectsInactive.Include);
            flow.panouTremor = tp != null ? tp.gameObject : null;

            // Înălțimea capului.
            var origin = Object.FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
            string inaltime = "nu am găsit XR Origin";
            if (origin != null)
            {
                if (origin.GetComponent<InaltimeJucator>() == null)
                {
                    var ij = Undo.AddComponent<InaltimeJucator>(origin.gameObject);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(ij);
                }
                inaltime = "InaltimeJucator pus pe XR Origin";
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = flowGo;
            Debug.Log($"[Flux] Gata: PanouFlux + Empatie Flow în SampleScene; meniu {(meniu ? "legat" : "LIPSĂ")}, " +
                      $"panou tremor {(tp ? "legat" : "LIPSĂ")}; {inaltime}. Scena salvată.");
        }

        // ---------------- construcția panoului ----------------

        static PanouFlux BuildPanel()
        {
            var root = new GameObject("PanouFlux", typeof(RectTransform));
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            root.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            root.AddComponent<TrackedDeviceGraphicRaycaster>();
            root.AddComponent<GraphicRaycaster>();
            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(W, H);
            rt.localScale = Vector3.one * 0.001f;

            var p = root.AddComponent<PanouFlux>();

            // ---- Tutorial ----
            var tut = Page("Pagina_Tutorial", rt, true);
            p.tutorial = tut.gameObject;
            Text(tut, "Titlu", "Cum folosești controllerele", 40, FontStyles.Bold, 0, 185, 640, 60);
            Text(tut, "Grip", "<b>Grip</b> (butonul de sub degetul mijlociu) — apuci paharul și markerul", 26, FontStyles.Normal, 0, 95, 640, 70);
            Text(tut, "Trigger", "<b>Trigger</b> (sub degetul arătător) — apeși butoanele de pe panouri", 26, FontStyles.Normal, 0, 25, 640, 70);
            Text(tut, "Deget", "<b>Vârful controllerului</b> — apeși tastele, ca și cum ai apăsa cu degetul", 26, FontStyles.Normal, 0, -45, 640, 70);
            p.tutorialOk = Btn(tut, "Buton_AmInteles", "Am înțeles", 0, -170, 280, 80, Portocaliu);

            // ---- Alegerea tremorului ----
            var set = Page("Pagina_Setari", rt, true);
            p.setari = set.gameObject;
            Text(set, "Titlu", "Alege tremorul", 42, FontStyles.Bold, 0, 185, 640, 60);
            Text(set, "Text",
                "Folosește panoul de deasupra mesei ca să alegi <b>profilul</b> și <b>nivelul</b>. Îl simți deja în mână.\n" +
                "La fiecare stație faci o încercare <color=#3AA6A0><b>fără tremor</b></color> și una <color=#F26D5B><b>cu tremorul ales</b></color>.",
                26, FontStyles.Normal, 0, 75, 640, 150);
            p.setariAles = Text(set, "Ales", "Ales: Moderat · Parkinson (~5 Hz)", 30, FontStyles.Normal, 0, -40, 640, 50);
            p.setariContinua = Btn(set, "Buton_Continua", "Continuă", 0, -170, 280, 80, Portocaliu);

            // ---- Instrucțiuni stație ----
            var ins = Page("Pagina_Instructiuni", rt, true);
            p.instructiuni = ins.gameObject;
            p.instrStatie = Text(ins, "Statia", "Stația 1 din 3", 24, FontStyles.Normal, 0, 200, 640, 40);
            p.instrStatie.color = Gri;
            p.instrTitlu = Text(ins, "Titlu", "Toarnă apa", 46, FontStyles.Bold, 0, 150, 640, 60);
            p.instrText = Text(ins, "Text", "Ce ai de făcut...", 26, FontStyles.Normal, 0, 45, 640, 140);
            p.instrBadge = B.CreateImage("Badge", ins, Turcoaz);
            p.instrBadge.rectTransform.anchoredPosition = new Vector2(0, -65);
            p.instrBadge.rectTransform.sizeDelta = new Vector2(640, 60);
            p.instrIncercare = Text(p.instrBadge.rectTransform, "Text", "Încercarea 1 din 2 · fără tremor", 25, FontStyles.Bold, 0, 0, 630, 60);
            p.instrStart = Btn(ins, "Buton_Start", "Start", 0, -170, 280, 80, Portocaliu);

            // ---- Bara din timpul încercării (doar o fâșie sus, restul transparent) ----
            var bar = Page("Pagina_Bara", rt, false);
            p.bara = bar.gameObject;
            var barBg = B.CreateImage("Fundal", bar, Bg);
            barBg.rectTransform.anchoredPosition = new Vector2(0, 205);
            barBg.rectTransform.sizeDelta = new Vector2(W - 20, 64);
            p.baraText = Text(barBg.rectTransform, "Text", "Toarnă apa · încercarea 1 din 2", 22, FontStyles.Normal, -85, 0, 480, 56);
            p.baraText.alignment = TextAlignmentOptions.Left;
            p.baraSari = Btn(barBg.rectTransform, "Buton_Sari", "Sari peste", 245, 0, 160, 48, new Color(0.30f, 0.33f, 0.38f));

            // ---- Card informativ ----
            var crd = Page("Pagina_Card", rt, true);
            p.card = crd.gameObject;
            p.cardTitlu = Text(crd, "Titlu", "Știai că?", 42, FontStyles.Bold, 0, 185, 640, 60);
            p.cardText = Text(crd, "Text", "...", 27, FontStyles.Normal, 0, 55, 640, 200);
            p.cardSursa = Text(crd, "Sursa", "Sursa: ...", 20, FontStyles.Italic, 0, -85, 640, 40);
            p.cardSursa.color = Gri;
            p.cardContinua = Btn(crd, "Buton_Continua", "Continuă", 0, -170, 280, 80, Portocaliu);

            // ---- Rezultate ----
            var rez = Page("Pagina_Rezultate", rt, true);
            p.rezultate = rez.gameObject;
            Text(rez, "Titlu", "Rezultatele tale", 40, FontStyles.Bold, 0, 200, 640, 56);
            Text(rez, "Cap_Statie", "Stația", 22, FontStyles.Bold, -200, 145, 240, 36).color = Gri;
            Text(rez, "Cap_Fara", "Fără tremor", 22, FontStyles.Bold, 40, 145, 220, 36).color = Turcoaz;
            Text(rez, "Cap_Cu", "Cu tremor", 22, FontStyles.Bold, 250, 145, 200, 36).color = Coral;
            const int n = 4;
            p.rezRanduri = new GameObject[n];
            p.rezNume = new TMP_Text[n]; p.rezFara = new TMP_Text[n]; p.rezCu = new TMP_Text[n];
            for (int i = 0; i < n; i++)
            {
                var row = new GameObject($"Rand_{i + 1}", typeof(RectTransform));
                row.transform.SetParent(rez, false);
                var rr = (RectTransform)row.transform;
                rr.anchoredPosition = new Vector2(0, 98 - i * 46);
                rr.sizeDelta = new Vector2(W, 44);
                p.rezRanduri[i] = row;
                p.rezNume[i] = Text(rr, "Nume", "Stația", 22, FontStyles.Bold, -200, 0, 240, 44);
                p.rezFara[i] = Text(rr, "Fara", "—", 22, FontStyles.Normal, 40, 0, 220, 44);
                p.rezCu[i] = Text(rr, "Cu", "—", 22, FontStyles.Normal, 250, 0, 200, 44);
            }
            p.rezRezumat = Text(rez, "Rezumat", "...", 23, FontStyles.Normal, 0, -105, 640, 60);
            p.rezRefa = Btn(rez, "Buton_Refa", "Refă", -215, -185, 200, 70, Portocaliu);
            p.rezAltNivel = Btn(rez, "Buton_AltNivel", "Alt nivel", 0, -185, 200, 70, new Color(0.30f, 0.33f, 0.38f));
            p.rezMeniu = Btn(rez, "Buton_Meniu", "Meniu", 215, -185, 200, 70, new Color(0.30f, 0.33f, 0.38f));

            foreach (var page in new[] { p.setari, p.instructiuni, p.bara, p.card, p.rezultate }) page.SetActive(false);
            return p;
        }

        static RectTransform Page(string name, RectTransform parent, bool background)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform;
            B.Stretch(r);
            if (background) B.Stretch(B.CreateImage("Fundal", r, Bg).rectTransform);
            return r;
        }

        static TextMeshProUGUI Text(RectTransform parent, string name, string text, float size, FontStyles style,
                                    float x, float y, float w, float h)
        {
            var t = B.CreateText(name, parent, text, size, style, new Vector2(x, y), new Vector2(w, h));
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        static Button Btn(RectTransform parent, string name, string label, float x, float y, float w, float h, Color color)
        {
            var b = B.CreateButton(name, parent, label, new Vector2(x, y), new Vector2(w, h), out _);
            ((Image)b.targetGraphic).color = color;
            return b;
        }
    }
}
