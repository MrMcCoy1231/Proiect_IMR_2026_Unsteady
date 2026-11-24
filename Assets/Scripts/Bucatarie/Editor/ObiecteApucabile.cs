using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Unsteady.EditorTools
{
    /// Obiectele mici din decorul bucătăriei (Kenney: măr, banană, castron, farfurie, burger, ghiveci)
    /// devin apucabile: collider potrivit formei, Rigidbody și XR Grab Interactable.
    /// Lucrează direct pe prefab-ul Bucatarie_Decor (al Ancăi), deci se vede în orice scenă care îl folosește.
    public static class ObiecteApucabile
    {
        const string DecorPrefab = "Assets/Prefabs/Bucatarie/Bucatarie_Decor.prefab";

        enum Forma { Sfera, Cutie, Convex, Farfurie, Castron }

        struct Obiect
        {
            public string nume; public Forma forma; public float masa;
            public Obiect(string n, Forma f, float m) { nume = n; forma = f; masa = m; }
        }

        static readonly Obiect[] Lista =
        {
            new Obiect("apple", Forma.Sfera, 0.15f),
            new Obiect("banana", Forma.Convex, 0.12f),
            new Obiect("bowl", Forma.Castron, 0.35f),
            new Obiect("plate", Forma.Farfurie, 0.35f),
            new Obiect("burger-cheese-double", Forma.Cutie, 0.25f),
            new Obiect("plantSmall1", Forma.Cutie, 0.8f),
        };

        [MenuItem("Unsteady/12. Obiectele de pe blat apucabile (fizică + coliziuni)")]
        static void Aplica()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Unsteady", "Oprește Play întâi.", "OK");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(DecorPrefab) == null)
            {
                EditorUtility.DisplayDialog("Unsteady", "Lipsește " + DecorPrefab, "OK");
                return;
            }

            // ce s-a adăugat de mână în scenă pe aceste obiecte (Rigidbody, collidere, grab) scoatem,
            // altfel ar fi dublat de ce punem în prefab
            int scoase = CurataScena();

            var gasite = new List<string>();
            var lipsa = new List<string>();
            var root = PrefabUtility.LoadPrefabContents(DecorPrefab);
            try
            {
                foreach (var o in Lista)
                {
                    var t = root.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == o.nume);
                    if (t == null) { lipsa.Add(o.nume); continue; }
                    Configureaza(t.gameObject, o);
                    gasite.Add(o.nume);
                }
                PrefabUtility.SaveAsPrefabAsset(root, DecorPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            var scena = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scena);
            EditorSceneManager.SaveScene(scena);

            string msg = "Apucabile acum: " + string.Join(", ", gasite) + ".";
            if (scoase > 0) msg += $"\nAm scos {scoase} componente puse de mână în scenă (acum vin din prefab).";
            if (lipsa.Count > 0) msg += "\nNu le-am găsit: " + string.Join(", ", lipsa) + ".";
            msg += "\nApasă Play: le poți apuca cu grip (G în simulator), cad și se lovesc între ele.";
            Debug.Log("[Unsteady] " + msg);
            EditorUtility.DisplayDialog("Unsteady", msg, "OK");
        }

        static int CurataScena()
        {
            int n = 0;
            var scena = EditorSceneManager.GetActiveScene();
            var toate = scena.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToList();
            foreach (var o in Lista)
            {
                foreach (var t in toate.Where(x => x != null && x.name == o.nume))
                {
                    var go = t.gameObject;
                    if (!PrefabUtility.IsPartOfPrefabInstance(go)) continue;
                    // ordinea contează: grab-ul depinde de Rigidbody
                    var comps = go.GetComponentsInChildren<Component>(true)
                        .Where(c => c != null && PrefabUtility.IsAddedComponentOverride(c)
                                    && (c is XRBaseInteractable || c is Collider || c is Rigidbody))
                        .OrderBy(c => c is XRBaseInteractable ? 0 : c is Collider ? 1 : 2).ToList();
                    foreach (var c in comps) { Undo.DestroyObjectImmediate(c); n++; }
                    PrefabUtility.RevertObjectOverride(go, InteractionMode.AutomatedAction);   // ex. bifa Static
                }
            }
            return n;
        }

        static void Configureaza(GameObject go, Obiect o)
        {
            // scoatem colliderele vechi (unele erau mesh colliders ne-convexe, care nu merg cu Rigidbody)
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            foreach (var t in go.GetComponentsInChildren<Transform>(true).Where(x => x.name.StartsWith("Col_")).ToList())
                if (t != null) Object.DestroyImmediate(t.gameObject);

            if (!Limite(go, out Bounds b)) return;
            float s = Mathf.Max(1e-4f, Mathf.Abs(go.transform.lossyScale.x));

            switch (o.forma)
            {
                case Forma.Sfera:
                {
                    var c = CopilColider(go, "Col_Sfera", b.center, Quaternion.identity);
                    var sc = c.AddComponent<SphereCollider>();
                    sc.radius = Mathf.Max(b.extents.x, b.extents.z) * 0.95f / s;
                    break;
                }
                case Forma.Cutie:
                {
                    var c = CopilColider(go, "Col_Cutie", b.center, Quaternion.identity);
                    c.AddComponent<BoxCollider>().size = b.size / s;
                    break;
                }
                case Forma.Convex:
                {
                    foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (mf.sharedMesh == null) continue;
                        var mc = mf.gameObject.AddComponent<MeshCollider>();
                        mc.sharedMesh = mf.sharedMesh;
                        mc.convex = true;
                    }
                    break;
                }
                case Forma.Farfurie:
                {
                    // un disc plat jos: ce stă pe farfurie (burgerul) nu e împins în sus de margine
                    float h = Mathf.Max(b.size.y * 0.45f, 0.004f);
                    var c = CopilColider(go, "Col_Fund", new Vector3(b.center.x, b.min.y + h * 0.5f, b.center.z), Quaternion.identity);
                    c.AddComponent<BoxCollider>().size = new Vector3(b.size.x * 0.92f, h, b.size.z * 0.92f) / s;
                    break;
                }
                case Forma.Castron:
                {
                    // fund + pereți în cerc, ca mărul să stea ÎN castron, nu deasupra lui
                    float h = b.size.y;
                    float r = Mathf.Min(b.extents.x, b.extents.z);
                    float fund = Mathf.Max(h * 0.18f, 0.004f);
                    var cf = CopilColider(go, "Col_Fund", new Vector3(b.center.x, b.min.y + fund * 0.5f, b.center.z), Quaternion.identity);
                    cf.AddComponent<BoxCollider>().size = new Vector3(r * 1.3f, fund, r * 1.3f) / s;

                    const int N = 10;
                    float grosime = Mathf.Max(r * 0.12f, 0.004f);
                    float lung = 2f * r * Mathf.Tan(Mathf.PI / N) * 1.15f;
                    for (int i = 0; i < N; i++)
                    {
                        float a = i * Mathf.PI * 2f / N;
                        Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                        Vector3 poz = new Vector3(b.center.x, b.min.y + h * 0.5f, b.center.z) + dir * (r - grosime * 0.5f);
                        var cw = CopilColider(go, "Col_Perete" + i, poz, Quaternion.LookRotation(dir, Vector3.up));
                        cw.AddComponent<BoxCollider>().size = new Vector3(lung, h, grosime) / s;
                    }
                    break;
                }
            }

            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            rb.mass = o.masa;
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            rb.angularDamping = 0.3f;

            var grab = go.GetComponent<XRGrabInteractable>();
            if (grab == null) grab = go.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking; // se lovește de lucruri chiar și în mână
            grab.smoothPosition = false;      // fără netezire: tremorul se vede și pe obiecte
            grab.smoothRotation = false;
            grab.useDynamicAttach = true;     // îl ții de unde l-ai apucat
            grab.throwOnDetach = true;

            // obiectele de decor erau statice (lumina baked); acum se mișcă, deci nu mai pot fi statice
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
        }

        static GameObject CopilColider(GameObject parinte, string nume, Vector3 pozLume, Quaternion rotLume)
        {
            var c = new GameObject(nume);
            c.transform.SetParent(parinte.transform, false);
            c.transform.SetPositionAndRotation(pozLume, rotLume);
            return c;
        }

        static bool Limite(GameObject go, out Bounds b)
        {
            b = default;
            bool am = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!am) { b = r.bounds; am = true; } else b.Encapsulate(r.bounds);
            }
            return am;
        }
    }
}
