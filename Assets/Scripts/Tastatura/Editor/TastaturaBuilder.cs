using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using B = Unsteady.UI.EditorTools.TremorPanelBuilder;

namespace Unsteady.Tastatura.EditorTools
{
    /// <summary>
    /// Modelul Tastatura.fbx e o singură bucată, dar tastele sunt "insule" separate în ea.
    /// Scriptul le desparte în 61 de obiecte, le dă litere (QWERTY), scoate butoanele
    /// PokeButton vechi și pune un ecran cu sarcina de scris.
    /// </summary>
    static class TastaturaBuilder
    {
        const string PrefabPath = "Assets/Prefabs/Tastatura/Tastatura.prefab";
        const string FbxPath = "Assets/Models/Graybox/Tastatura.fbx";
        const string MeshAssetPath = "Assets/Prefabs/Tastatura/Tastatura_Taste.asset";
        const string KeyMatPath = "Assets/Materiale/Tasta.mat";
        const string BaseMeshName = "Tastatura_Baza";

        static readonly string[][] Layout =
        {
            new[] { "`", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=", "Back" },
            new[] { "Tab", "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "[", "]", "\\" },
            new[] { "Caps", "A", "S", "D", "F", "G", "H", "J", "K", "L", ";", "'", "Enter" },
            new[] { "Shift", "Z", "X", "C", "V", "B", "N", "M", ",", ".", "/", "Shift" },
            new[] { "Ctrl", "Win", "Alt", "Space", "Alt", "Win", "Menu", "Ctrl" },
        };

        class Island
        {
            public List<int> tris = new List<int>();
            public Bounds bounds;
        }

        [MenuItem("Unsteady/5. Fă tastatura funcțională (taste reale)")]
        static void Build()
        {
            var orig = AssetDatabase.LoadAllAssetsAtPath(FbxPath).OfType<Mesh>().FirstOrDefault();
            if (orig == null || AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                EditorUtility.DisplayDialog("Unsteady", "Lipsește Tastatura.fbx sau prefabul Tastatura.", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                if (!BuildInto(root, orig)) return;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[Tastatura] Gata: taste reale + ecran. Prefabul Tastatura e actualizat în toate scenele.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static bool BuildInto(GameObject root, Mesh orig)
        {
            // 1) Curăță: butoanele PokeButton vechi și o construcție anterioară.
            foreach (var t in root.GetComponentsInChildren<Transform>(true).ToArray())
            {
                if (t == null || t.gameObject == root) continue;
                var src = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                bool pokeButton = PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject) && src != null && src.name == "PokeButton";
                if (pokeButton || t.name == "Taste" || t.name == "Ecran_Tastatura")
                    Object.DestroyImmediate(t.gameObject);
            }
            foreach (var c in root.GetComponentsInChildren<TastaturaFunctionala>(true))
                Object.DestroyImmediate(c);

            var mf = root.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m =>
                m.sharedMesh == orig || (m.sharedMesh != null && AssetDatabase.GetAssetPath(m.sharedMesh) == MeshAssetPath));
            if (mf == null)
                mf = root.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m =>
                    AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromOriginalSource(m.gameObject)) == FbxPath);
            if (mf == null)
            {
                foreach (var m in root.GetComponentsInChildren<MeshFilter>(true))
                    Debug.Log($"[Tastatura] MeshFilter pe {m.name}: mesh={(m.sharedMesh ? m.sharedMesh.name : "null")} path={AssetDatabase.GetAssetPath(m.sharedMesh)}");
                EditorUtility.DisplayDialog("Unsteady", "Nu am găsit mesh-ul tastaturii în prefab.", "OK");
                return false;
            }

            // 2) Desparte mesh-ul în insule (bucăți neconectate).
            var islands = SplitIslands(orig);
            var baseIsland = islands.OrderByDescending(i => i.bounds.size.sqrMagnitude).First();
            var keyIslands = islands.Where(i => i != baseIsland).ToList();
            if (keyIslands.Count < 10)
            {
                EditorUtility.DisplayDialog("Unsteady", $"Am găsit doar {keyIslands.Count} taste în model.", "OK");
                return false;
            }

            // 3) Axe: "sus" = axa pe care toate tastele au aceeași înălțime.
            var centers = keyIslands.Select(i => i.bounds.center).ToList();
            int upAxis = Enumerable.Range(0, 3).OrderBy(a => Variance(centers.Select(c => c[a]))).First();
            int[] others = Enumerable.Range(0, 3).Where(a => a != upAxis).ToArray();
            float std = Median(keyIslands.Select(i => Mathf.Min(i.bounds.size[others[0]], i.bounds.size[others[1]])));
            // Axa rândurilor: pe ea tastele au doar câteva poziții distincte (5 rânduri).
            int rowAxis = others.OrderBy(a => centers.Select(c => Mathf.RoundToInt(c[a] / (std * 0.15f))).Distinct().Count()).First();
            int colAxis = others.First(a => a != rowAxis);

            Vector3 U = Axis(upAxis) * Mathf.Sign(centers[0][upAxis] - baseIsland.bounds.center[upAxis]);

            // 4) Rânduri.
            var rows = GroupRows(keyIslands, rowAxis, std * 0.5f);
            int Wide(List<Island> r) => r.Count(i => i.bounds.size[colAxis] > std * 1.3f);
            var numberRow = rows.FirstOrDefault(r => r.Count == 14 && Wide(r) == 1);
            var tabRow = rows.FirstOrDefault(r => r.Count == 14 && Wide(r) == 2);
            var homeRow = rows.FirstOrDefault(r => r.Count == 13);
            var shiftRow = rows.FirstOrDefault(r => r.Count == 12);
            var bottomRow = rows.FirstOrDefault(r => r.Count == 8);
            bool layoutOk = numberRow != null && tabRow != null && homeRow != null && shiftRow != null && bottomRow != null;

            Vector3 R = Axis(colAxis), F = Axis(rowAxis);
            if (layoutOk)
            {
                var wide = numberRow.First(i => i.bounds.size[colAxis] > std * 1.3f);
                float mean = numberRow.Average(i => i.bounds.center[colAxis]);
                R *= Mathf.Sign(wide.bounds.center[colAxis] - mean);           // Backspace e în dreapta
                F *= Mathf.Sign(numberRow[0].bounds.center[rowAxis] - bottomRow[0].bounds.center[rowAxis]); // cifrele sunt în spate
            }
            else Debug.LogWarning("[Tastatura] Nu am recunoscut rândurile QWERTY. Tastele merg, dar fără litere.");

            // 5) Mesh-uri noi: baza + câte unul pe tastă.
            if (AssetDatabase.LoadMainAssetAtPath(MeshAssetPath) != null) AssetDatabase.DeleteAsset(MeshAssetPath);
            var baseMesh = MakeMesh(orig, baseIsland, Vector3.zero, BaseMeshName);
            AssetDatabase.CreateAsset(baseMesh, MeshAssetPath);
            mf.sharedMesh = baseMesh;
            var mc = mf.GetComponent<MeshCollider>();
            if (mc != null) mc.sharedMesh = baseMesh;

            var keyMat = GetKeyMaterial();
            var holder = new GameObject("Taste").transform;
            holder.SetParent(mf.transform, false);

            var labels = new Dictionary<Island, string>();
            if (layoutOk)
            {
                var named = new[] { numberRow, tabRow, homeRow, shiftRow, bottomRow };
                for (int r = 0; r < 5; r++)
                {
                    var sorted = named[r].OrderBy(i => Vector3.Dot(i.bounds.center, R)).ToList();
                    for (int c = 0; c < sorted.Count; c++) labels[sorted[c]] = Layout[r][c];
                }
            }

            var keys = new List<Tasta>();
            int n = 0;
            foreach (var isl in keyIslands.OrderByDescending(i => Vector3.Dot(i.bounds.center, F)).ThenBy(i => Vector3.Dot(i.bounds.center, R)))
            {
                labels.TryGetValue(isl, out string label);
                label = label ?? "";
                var mesh = MakeMesh(orig, isl, isl.bounds.center, $"Tasta_{n:00}_{label}");
                AssetDatabase.AddObjectToAsset(mesh, MeshAssetPath);

                var go = new GameObject($"Tasta_{SafeName(label, n)}");
                go.transform.SetParent(holder, false);
                go.transform.localPosition = isl.bounds.center;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = keyMat;
                go.AddComponent<BoxCollider>(); // pentru raza controllerului

                var k = go.AddComponent<Tasta>();
                k.label = label;
                SetKind(k, label);
                k.restLocalPos = isl.bounds.center;
                k.halfRight = Mathf.Abs(Vector3.Dot(isl.bounds.extents, R));
                k.halfUp = Mathf.Abs(Vector3.Dot(isl.bounds.extents, U));
                k.halfForward = Mathf.Abs(Vector3.Dot(isl.bounds.extents, F));
                AddLabel(go.transform, k, U, F);
                keys.Add(k);
                n++;
            }

            // 6) Scriptul și ecranul.
            var kb = root.AddComponent<TastaturaFunctionala>();
            kb.keySpace = holder;
            kb.up = U; kb.right = R; kb.forward = F;
            kb.keys = keys.ToArray();
            BuildScreen(kb, holder, baseIsland.bounds, U, R, F);

            AssetDatabase.SaveAssets();
            return true;
        }

        // ---------------- mesh ----------------

        static List<Island> SplitIslands(Mesh m)
        {
            var v = m.vertices;
            var tris = new List<int>();
            for (int s = 0; s < m.subMeshCount; s++) tris.AddRange(m.GetTriangles(s));

            // Sudează vârfurile cu aceeași poziție (Unity le dublează pe muchii).
            var map = new Dictionary<Vector3Int, int>();
            var pid = new int[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                var q = new Vector3Int(Mathf.RoundToInt(v[i].x * 10000f), Mathf.RoundToInt(v[i].y * 10000f), Mathf.RoundToInt(v[i].z * 10000f));
                if (!map.TryGetValue(q, out int id)) { id = map.Count; map[q] = id; }
                pid[i] = id;
            }
            var parent = Enumerable.Range(0, map.Count).ToArray();
            int Find(int a) { while (parent[a] != a) { parent[a] = parent[parent[a]]; a = parent[a]; } return a; }
            void Union(int a, int b) { a = Find(a); b = Find(b); if (a != b) parent[a] = b; }
            for (int t = 0; t < tris.Count; t += 3)
            {
                Union(pid[tris[t]], pid[tris[t + 1]]);
                Union(pid[tris[t]], pid[tris[t + 2]]);
            }

            var byRoot = new Dictionary<int, Island>();
            for (int t = 0; t < tris.Count; t += 3)
            {
                int r = Find(pid[tris[t]]);
                if (!byRoot.TryGetValue(r, out var isl)) { isl = new Island(); isl.bounds = new Bounds(v[tris[t]], Vector3.zero); byRoot[r] = isl; }
                for (int j = 0; j < 3; j++) { isl.tris.Add(tris[t + j]); isl.bounds.Encapsulate(v[tris[t + j]]); }
            }
            return byRoot.Values.ToList();
        }

        static Mesh MakeMesh(Mesh src, Island isl, Vector3 offset, string name)
        {
            var sv = src.vertices; var sn = src.normals; var suv = src.uv;
            var remap = new Dictionary<int, int>();
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>();
            var tris = new List<int>();
            foreach (int i in isl.tris)
            {
                if (!remap.TryGetValue(i, out int j))
                {
                    j = verts.Count; remap[i] = j;
                    verts.Add(sv[i] - offset);
                    if (sn.Length == sv.Length) norms.Add(sn[i]);
                    if (suv.Length == sv.Length) uvs.Add(suv[i]);
                }
                tris.Add(j);
            }
            var m = new Mesh { name = name };
            m.SetVertices(verts);
            if (norms.Count == verts.Count) m.SetNormals(norms);
            if (uvs.Count == verts.Count) m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            if (norms.Count != verts.Count) m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // ---------------- taste ----------------

        static void SetKind(Tasta k, string label)
        {
            switch (label)
            {
                case "Space": k.kind = KeyKind.Spatiu; k.character = ' '; break;
                case "Back": k.kind = KeyKind.Stergere; break;
                case "Enter": k.kind = KeyKind.Enter; break;
                default:
                    if (label.Length == 1) { k.kind = KeyKind.Caracter; k.character = label[0]; }
                    else k.kind = KeyKind.Modificator;
                    break;
            }
        }

        static void AddLabel(Transform key, Tasta k, Vector3 U, Vector3 F)
        {
            string text = k.label == "Back" ? "Bksp" : k.label == "Space" ? "" : k.label;
            if (string.IsNullOrEmpty(text)) return;
            var go = new GameObject("Eticheta", typeof(RectTransform));
            go.transform.SetParent(key, false);
            go.transform.localPosition = U * (k.halfUp * 1.08f);
            go.transform.localRotation = Quaternion.LookRotation(-U, F);
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = text;
            tmp.color = new Color(0.15f, 0.15f, 0.17f);
            tmp.alignment = TextAlignmentOptions.Center;
            // Mărimea fontului TMP 3D e în "puncte" (~0.1 unități per punct), deci o raportăm la mărimea tastei.
            float keyUnits = Mathf.Min(k.halfRight, k.halfForward) * 2f;
            tmp.enableAutoSizing = false;
            tmp.fontSize = keyUnits * (text.Length == 1 ? 6f : 2.4f);
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            ((RectTransform)go.transform).sizeDelta = new Vector2(k.halfRight * 1.6f, k.halfForward * 1.6f);
        }

        static void BuildScreen(TastaturaFunctionala kb, Transform holder, Bounds baseB, Vector3 U, Vector3 R, Vector3 F)
        {
            float mpu = holder.TransformVector(U).magnitude;
            float upm = mpu > 0 ? 1f / mpu : 1f; // unități per metru

            var go = new GameObject("Ecran_Tastatura", typeof(RectTransform));
            go.transform.SetParent(holder, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            go.AddComponent<UnityEngine.UI.CanvasScaler>().dynamicPixelsPerUnit = 10f;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(380, 150);

            float halfF = Mathf.Abs(Vector3.Dot(baseB.extents, F));
            float halfU = Mathf.Abs(Vector3.Dot(baseB.extents, U));
            rt.localPosition = baseB.center + F * (halfF + 0.03f * upm) + U * (halfU + 0.10f * upm);
            rt.localRotation = Quaternion.LookRotation(F, U);
            rt.localScale = Vector3.one * 0.001f * upm;

            var bg = B.CreateImage("Fundal", rt, new Color(0.08f, 0.09f, 0.11f, 0.92f));
            B.Stretch(bg.rectTransform);
            kb.targetText = B.CreateText("Cuvant", rt, "Scrie: CASA", 34, FontStyles.Normal, new Vector2(0, 45), new Vector2(360, 45));
            kb.typedText = B.CreateText("Scris", rt, "_ _ _ _", 40, FontStyles.Bold, new Vector2(0, -2), new Vector2(360, 50));
            kb.statsText = B.CreateText("Statistici", rt, "Apasă tastele cu degetul", 20, FontStyles.Normal, new Vector2(0, -50), new Vector2(360, 45));
        }

        static Material GetKeyMaterial()
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(KeyMatPath);
            if (m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", new Color(0.92f, 0.92f, 0.90f));
            if (!AssetDatabase.IsValidFolder("Assets/Materiale")) AssetDatabase.CreateFolder("Assets", "Materiale");
            AssetDatabase.CreateAsset(m, KeyMatPath);
            return m;
        }

        // ---------------- utilitare ----------------

        static Vector3 Axis(int a) => a == 0 ? Vector3.right : a == 1 ? Vector3.up : Vector3.forward;

        static float Variance(IEnumerable<float> xs)
        {
            var l = xs.ToList(); float mean = l.Average();
            return l.Sum(x => (x - mean) * (x - mean)) / l.Count;
        }

        static float Median(IEnumerable<float> xs)
        {
            var l = xs.OrderBy(x => x).ToList();
            return l[l.Count / 2];
        }

        static int CountGroups(IEnumerable<float> xs, float gap)
        {
            var l = xs.OrderBy(x => x).ToList();
            int g = 1;
            for (int i = 1; i < l.Count; i++) if (l[i] - l[i - 1] > gap) g++;
            return g;
        }

        static List<List<Island>> GroupRows(List<Island> keys, int axis, float gap)
        {
            var sorted = keys.OrderBy(k => k.bounds.center[axis]).ToList();
            var rows = new List<List<Island>> { new List<Island> { sorted[0] } };
            for (int i = 1; i < sorted.Count; i++)
            {
                if (sorted[i].bounds.center[axis] - sorted[i - 1].bounds.center[axis] > gap) rows.Add(new List<Island>());
                rows[rows.Count - 1].Add(sorted[i]);
            }
            return rows;
        }

        static string SafeName(string label, int n)
        {
            switch (label)
            {
                case "": return n.ToString("00");
                case "`": return "Backtick"; case "-": return "Minus"; case "=": return "Egal";
                case "[": return "ParantezaSt"; case "]": return "ParantezaDr"; case "\\": return "Backslash";
                case ";": return "PunctVirgula"; case "'": return "Apostrof"; case ",": return "Virgula";
                case ".": return "Punct"; case "/": return "Slash";
                default: return label;
            }
        }
    }
}
