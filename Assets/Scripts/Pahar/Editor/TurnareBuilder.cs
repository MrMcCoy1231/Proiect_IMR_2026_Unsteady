using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Unsteady.EditorTools
{
    /// Integrează stația de turnat (Ștefan, PR #4) în SampleScene:
    ///  1. prefab-ul Pahar folosește scriptul nou Pahar (în loc de TurnareApa) + jetul de apă;
    ///  2. paharul țintă devine tot un Pahar (în locul vechiului Pahar_Tinta);
    ///  3. adaugă inelul (SemnNivel) și stația (Statie_Turnare) în scenă;
    ///  4. curăță scena Dev_Stefan și fișierele în plus din PR.
    public static class TurnareBuilder
    {
        const string PaharPrefab = "Assets/Prefabs/Pahar/Pahar.prefab";
        const string MatJet = "Assets/Materiale/Apa_Jet.mat";
        const string MatInel = "Assets/Materiale/Inel_Semn.mat";
        const string Scena = "Assets/Scenes/SampleScene.unity";
        const string ScenaDev = "Assets/Scenes/Dev/Dev_Stefan.unity";
        static readonly string[] FisiereInPlus =
        {
            "Assets/Prefabs/Pahar/Pahar (1).prefab",
            "Assets/Materiale/Texturi/Materials/abstract-textured-backgound.mat",
            "Assets/Materiale/Texturi/abstract-textured-backgound.jpg",
        };
        const string FolderInPlus = "Assets/Materiale/Texturi/Materials";

        [MenuItem("Unsteady/10. Integrează stația de turnat (Ștefan) în SampleScene")]
        static void Integreaza()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var matJet = CreeazaMaterial(MatJet, Color.white);
            var matInel = CreeazaMaterial(MatInel, Color.white);
            if (matJet == null || matInel == null)
            {
                EditorUtility.DisplayDialog("Unsteady", "Nu găsesc shaderul Sprites/Default.", "OK");
                return;
            }

            if (!ActualizeazaPrefab(matJet)) return;
            ReparaScenaDev();
            StergeFisiereInPlus();
            string raport = IntegreazaInScena(matInel);

            Debug.Log("[Unsteady] " + raport);
            EditorUtility.DisplayDialog("Unsteady", raport, "OK");
        }

        // ---------------- materiale ----------------

        static Material CreeazaMaterial(string cale, Color c)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(cale);
            if (m != null) return m;
            var sh = Shader.Find("Sprites/Default");
            if (sh == null) return null;
            m = new Material(sh) { color = c };
            AssetDatabase.CreateAsset(m, cale);
            AssetDatabase.SaveAssets();
            return m;
        }

        // ---------------- prefab-ul paharului ----------------

        static bool ActualizeazaPrefab(Material matJet)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PaharPrefab) == null)
            {
                EditorUtility.DisplayDialog("Unsteady", "Lipsește " + PaharPrefab + ". Ai făcut Pull?", "OK");
                return false;
            }

            var root = PrefabUtility.LoadPrefabContents(PaharPrefab);
            try
            {
                StergeScripturiVechi(root);

                var p = root.GetComponent<Pahar>();
                if (p == null) p = root.AddComponent<Pahar>();

                if (p.apaVizual == null)
                {
                    var t = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "apa_vizual");
                    if (t != null) p.apaVizual = t;
                }
                if (p.particule == null) p.particule = root.GetComponentInChildren<ParticleSystem>(true);

                if (p.gura == null)
                {
                    var g = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "gura");
                    if (g == null)
                    {
                        g = new GameObject("gura").transform;
                        g.SetParent(root.transform, false);
                        if (Limite(root, p.apaVizual, out Bounds b))
                            g.position = new Vector3(b.center.x, b.max.y, b.center.z);
                    }
                    p.gura = g;
                }

                p.materialJet = matJet;
                p.razaGuraAutomata = true;
                p.nivelInitial = 0f;

                var rb = root.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    rb.mass = 0.2f;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                }

                PrefabUtility.SaveAsPrefabAsset(root, PaharPrefab);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static bool Limite(GameObject root, Transform exclus, out Bounds b)
        {
            b = default;
            bool am = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is LineRenderer) continue;
                if (exclus != null && r.transform.IsChildOf(exclus)) continue;
                if (!am) { b = r.bounds; am = true; } else b.Encapsulate(r.bounds);
            }
            return am;
        }

        static int StergeScripturiVechi(GameObject root)
        {
            int n = 0;
            foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;
                string tip = mb.GetType().Name;
                if (tip == "TurnareApa" || tip == "PaharTinta") { Object.DestroyImmediate(mb, true); n++; }
            }
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                n += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            return n;
        }

        // ---------------- scena lui Ștefan ----------------

        static void ReparaScenaDev()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenaDev) == null) return;
            var scena = EditorSceneManager.OpenScene(ScenaDev, OpenSceneMode.Single);

            // prefab-ul are acum propriul Pahar: scoatem Pahar-ul adăugat de mână pe instanțe (ar curge de două ori)
            foreach (var go in scena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject))
            {
                var pahare = go.GetComponents<Pahar>();
                if (pahare.Length < 2) continue;
                var dinPrefab = pahare.FirstOrDefault(x => !PrefabUtility.IsAddedComponentOverride(x));
                var adaugat = pahare.FirstOrDefault(x => PrefabUtility.IsAddedComponentOverride(x));
                if (dinPrefab == null || adaugat == null) continue;
                if (adaugat.apaVizual != null) dinPrefab.apaVizual = adaugat.apaVizual;
                if (adaugat.particule != null) dinPrefab.particule = adaugat.particule;
                dinPrefab.nivelInitial = adaugat.nivelInitial;
                PrefabUtility.RecordPrefabInstancePropertyModifications(dinPrefab);
                Object.DestroyImmediate(adaugat);
            }

            // textura fără sursă: paharul revine la materialul din prefab
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!r.sharedMaterials.Any(m => m != null && m.name.StartsWith("abstract-textured"))) continue;
                var so = new SerializedObject(r);
                var prop = so.FindProperty("m_Materials");
                if (PrefabUtility.IsPartOfPrefabInstance(r) && prop != null)
                    PrefabUtility.RevertPropertyOverride(prop, InteractionMode.AutomatedAction);
            }

            // stația lui pointa spre componentele scoase: le legăm din nou
            foreach (var s in Object.FindObjectsByType<StatieTurnare>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (s.paharPlin == null) s.paharPlin = GasestePahar(scena, "Pahar");
                if (s.paharGol == null) s.paharGol = GasestePahar(scena, "Pahar (1)");
                EditorUtility.SetDirty(s);
            }

            EditorSceneManager.MarkSceneDirty(scena);
            EditorSceneManager.SaveScene(scena);
        }

        static Pahar GasestePahar(UnityEngine.SceneManagement.Scene scena, string nume)
        {
            foreach (var r in scena.GetRootGameObjects())
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                    if (t.name == nume) { var p = t.GetComponent<Pahar>(); if (p != null) return p; }
            return null;
        }

        static void StergeFisiereInPlus()
        {
            foreach (var f in FisiereInPlus)
                if (AssetDatabase.LoadMainAssetAtPath(f) != null) AssetDatabase.DeleteAsset(f);
            if (AssetDatabase.IsValidFolder(FolderInPlus) && AssetDatabase.FindAssets("", new[] { FolderInPlus }).Length == 0)
                AssetDatabase.DeleteAsset(FolderInPlus);
            AssetDatabase.Refresh();
        }

        // ---------------- SampleScene ----------------

        static string IntegreazaInScena(Material matInel)
        {
            var scena = EditorSceneManager.OpenScene(Scena, OpenSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PaharPrefab);

            var plinGo = GasesteRadacinaDinPrefab(scena, "Pahar", PaharPrefab);
            if (plinGo == null) return "Nu găsesc paharul „Pahar” în SampleScene. Rulează întâi meniul 6.";
            StergeScripturiVechi(plinGo);
            var plin = plinGo.GetComponent<Pahar>();
            if (plin == null) return "Paharul din scenă nu are scriptul Pahar. Verifică prefab-ul.";
            plin.nivelInitial = 1f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(plin);

            // paharul țintă: o instanță a aceluiași prefab, pusă unde era vechiul Pahar_Tinta
            var tintaGo = GasesteRadacinaDinPrefab(scena, "Pahar_Tinta", PaharPrefab);
            string notaTinta = "paharul țintă era deja actualizat";
            if (tintaGo == null)
            {
                var vechi = scena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                                 .FirstOrDefault(t => t.name == "Pahar_Tinta");
                tintaGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scena);
                tintaGo.transform.SetParent(plinGo.transform.parent, false);
                tintaGo.transform.localScale = plinGo.transform.localScale;
                tintaGo.transform.rotation = plinGo.transform.rotation;
                Vector3 poz = vechi != null ? vechi.position : plinGo.transform.position + Vector3.right * 0.162f;
                poz.y = plinGo.transform.position.y;
                tintaGo.transform.position = poz;
                tintaGo.name = "Pahar_Tinta";
                if (vechi != null) Object.DestroyImmediate(vechi.gameObject);
                notaTinta = "paharul țintă a fost înlocuit cu un Pahar nou";
            }
            var tinta = tintaGo.GetComponent<Pahar>();
            tinta.nivelInitial = 0f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(tinta);

            // inelul
            var semnGo = GasesteSauCreeaza(scena, "SemnNivel");
            var lr = semnGo.GetComponent<LineRenderer>();
            if (lr == null) lr = semnGo.AddComponent<LineRenderer>();
            lr.sharedMaterial = matInel;
            lr.loop = true;
            lr.useWorldSpace = true;
            lr.widthMultiplier = 0.003f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            var inel = semnGo.GetComponent<InelSemn>();
            if (inel == null) inel = semnGo.AddComponent<InelSemn>();
            inel.pahar = tinta;
            inel.materialInel = matInel;

            // stația
            var statieGo = GasesteSauCreeaza(scena, "Statie_Turnare");
            var statie = statieGo.GetComponent<StatieTurnare>();
            if (statie == null) statie = statieGo.AddComponent<StatieTurnare>();
            statie.paharPlin = plin;
            statie.paharGol = tinta;
            statie.semn = semnGo.transform;
            statie.radacinaStatie = null;
            statie.testFaraFlux = false;
            inel.statie = statie;

            foreach (var o in new Object[] { lr, inel, statie }) EditorUtility.SetDirty(o);
            EditorSceneManager.MarkSceneDirty(scena);
            EditorSceneManager.SaveScene(scena);
            Selection.activeGameObject = statieGo;

            return "Stația de turnat e în SampleScene: Pahar (plin) → Pahar_Tinta (gol), inel verde la "
                 + $"{statie.nivelSemn:P0} ± {statie.margineEroare:P0}, {notaTinta}. Apasă Play și pornește modul Empatie.";
        }

        static GameObject GasesteRadacinaDinPrefab(UnityEngine.SceneManagement.Scene scena, string nume, string prefabPath)
        {
            foreach (var r in scena.GetRootGameObjects())
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != nume) continue;
                    if (!PrefabUtility.IsAnyPrefabInstanceRoot(t.gameObject)) continue;
                    var sursa = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                    if (sursa != null && AssetDatabase.GetAssetPath(sursa) == prefabPath) return t.gameObject;
                }
            return null;
        }

        static GameObject GasesteSauCreeaza(UnityEngine.SceneManagement.Scene scena, string nume)
        {
            var go = scena.GetRootGameObjects().FirstOrDefault(g => g.name == nume);
            if (go != null) return go;
            go = new GameObject(nume);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scena);
            return go;
        }
    }
}
