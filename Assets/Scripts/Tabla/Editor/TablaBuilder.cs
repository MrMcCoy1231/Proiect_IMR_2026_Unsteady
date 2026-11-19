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
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
            grab.smoothPosition = false;
            grab.smoothRotation = false;
            grab.useDynamicAttach = true;      // rămâne în mână cum l-ai apucat
            grab.throwOnDetach = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(rb);
            PrefabUtility.RecordPrefabInstancePropertyModifications(grab);

            // Vârful markerului: capătul mai subțire al axei lungi a modelului.
            var varf = CreeazaVarf(marker);

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
            return go.transform;
        }
    }
}
