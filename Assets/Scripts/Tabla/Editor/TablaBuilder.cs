using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Unsteady.Tabla.EditorTools
{
    /// <summary>
    /// Face tabla funcțională în SampleScene: markerul devine apucabil, primește un punct pe vârf,
    /// iar Tabla primește TablaScris (stația 3 din modul Empatie).
    /// </summary>
    static class TablaBuilder
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";

        [MenuItem("Unsteady/8. Fă tabla funcțională (scris cu markerul) în SampleScene")]
        static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var tabla = GameObject.Find("Tabla");
            var marker = GameObject.Find("Marker");
            if (tabla == null || marker == null)
            {
                EditorUtility.DisplayDialog("Unsteady", "Nu am găsit obiectele 'Tabla' și 'Marker' în SampleScene.", "OK");
                return;
            }

            // Tabla: collider (ca markerul să nu treacă prin ea când e lăsat liber).
            var tablaMesh = tabla.GetComponentInChildren<MeshFilter>();
            var tablaGo = tablaMesh != null ? tablaMesh.gameObject : tabla;
            if (tablaGo.GetComponent<Collider>() == null) Undo.AddComponent<BoxCollider>(tablaGo);

            // Markerul: apucabil, urmărește exact mâna (inclusiv tremorul).
            var rb = marker.GetComponent<Rigidbody>();
            if (rb == null) rb = Undo.AddComponent<Rigidbody>(marker);
            Undo.RecordObject(rb, "Rigidbody marker");
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            if (marker.GetComponentInChildren<Collider>() == null)
            {
                var mmf = marker.GetComponentInChildren<MeshFilter>();
                Undo.AddComponent<CapsuleCollider>(mmf != null ? mmf.gameObject : marker);
            }
            var grab = marker.GetComponent<XRGrabInteractable>();
            if (grab == null) grab = Undo.AddComponent<XRGrabInteractable>(marker);
            Undo.RecordObject(grab, "Grab marker");
            // Velocity Tracking: markerul urmărește mâna prin fizică, deci se oprește în tablă în loc să treacă prin ea.
            grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            grab.smoothPosition = false;
            grab.smoothRotation = false;
            grab.useDynamicAttach = false;     // se așază mereu la fel în mână, ca un pix
            grab.throwOnDetach = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(rb);
            PrefabUtility.RecordPrefabInstancePropertyModifications(grab);

            // Vârful markerului: capătul mai subțire al axei lungi a modelului.
            var varf = CreeazaVarf(marker);
            CreeazaPrindere(marker, varf, grab);

            var scris = tabla.GetComponent<TablaScris>();
            if (scris == null) scris = Undo.AddComponent<TablaScris>(tabla);
            Undo.RecordObject(scris, "TablaScris");
            scris.marker = marker.transform;
            scris.varf = varf;
            PrefabUtility.RecordPrefabInstancePropertyModifications(scris);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = varf.gameObject;
            Debug.Log("[Tabla] Gata: markerul e apucabil, are 'Varf', iar Tabla are TablaScris. Verifică în Scene view " +
                      "că punctul 'Varf' (selectat acum) e pe vârful markerului. SampleScene salvată.");
        }

        static Transform CreeazaVarf(GameObject marker)
        {
            var mf = marker.GetComponentInChildren<MeshFilter>();
            var parent = mf != null ? mf.transform : marker.transform;
            var existent = parent.Find("Varf");
            if (existent != null) Undo.DestroyObjectImmediate(existent.gameObject);

            Vector3 local = Vector3.zero;
            if (mf != null && mf.sharedMesh != null)
            {
                var b = mf.sharedMesh.bounds;
                var v = mf.sharedMesh.vertices;
                int ax = 0;
                for (int i = 1; i < 3; i++) if (b.size[i] > b.size[ax]) ax = i;
                float lung = b.size[ax], min = b.min[ax], max = b.max[ax];

                // Raza medie a vârfurilor de la fiecare capăt: vârful e capătul mai subțire.
                float razaMin = 0, razaMax = 0; int nMin = 0, nMax = 0;
                foreach (var p in v)
                {
                    Vector3 d = p - b.center; d[ax] = 0; float r = d.magnitude;
                    if (p[ax] < min + lung * 0.12f) { razaMin += r; nMin++; }
                    if (p[ax] > max - lung * 0.12f) { razaMax += r; nMax++; }
                }
                razaMin = nMin > 0 ? razaMin / nMin : 0; razaMax = nMax > 0 ? razaMax / nMax : 0;
                local = b.center; local[ax] = razaMax <= razaMin ? max : min;
            }

            var go = new GameObject("Varf");
            Undo.RegisterCreatedObjectUndo(go, "Varf");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;

            // Vârful colorat (albastru închis), ca să se vadă cu ce capăt scrii.
            var bila = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bila.name = "Varf_Vizual";
            Object.DestroyImmediate(bila.GetComponent<Collider>());
            bila.transform.SetParent(go.transform, false);
            Vector3 ls = go.transform.lossyScale;
            float diam = 0.016f;                                // ~1.6 cm diametru, indiferent de scara markerului
            bila.transform.localScale = new Vector3(diam / Mathf.Max(ls.x, 1e-5f), diam / Mathf.Max(ls.y, 1e-5f), diam / Mathf.Max(ls.z, 1e-5f));
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", new Color(0.12f, 0.20f, 0.55f));
            const string matPath = "Assets/Materiale/Marker_Varf.mat";
            var existentMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existentMat == null) { AssetDatabase.CreateAsset(mat, matPath); existentMat = mat; }
            bila.GetComponent<Renderer>().sharedMaterial = existentMat;
            return go.transform;
        }

        // Punctul de prindere: în treimea din spate a markerului, cu "înainte" spre vârf.
        // XRI aliniază acest punct cu mâna, deci markerul stă mereu ca un pix, cu vârful în direcția controllerului.
        static void CreeazaPrindere(GameObject marker, Transform varf, XRGrabInteractable grab)
        {
            var mf = marker.GetComponentInChildren<MeshFilter>();
            var parent = mf != null ? mf.transform : marker.transform;
            var existent = parent.Find("Prindere");
            if (existent != null) Undo.DestroyObjectImmediate(existent.gameObject);

            Vector3 centru = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds.center : Vector3.zero;
            Vector3 tip = varf.localPosition;
            Vector3 spate = centru - (tip - centru);                // capătul opus vârfului
            Vector3 local = Vector3.Lerp(spate, tip, 0.3f);

            var go = new GameObject("Prindere");
            Undo.RegisterCreatedObjectUndo(go, "Prindere");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            Vector3 axa = (tip - spate).normalized;
            Vector3 up = Mathf.Abs(Vector3.Dot(axa, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up;
            go.transform.localRotation = Quaternion.LookRotation(axa, up);

            Undo.RecordObject(grab, "Attach marker");
            grab.attachTransform = go.transform;
            PrefabUtility.RecordPrefabInstancePropertyModifications(grab);
        }
    }
}
