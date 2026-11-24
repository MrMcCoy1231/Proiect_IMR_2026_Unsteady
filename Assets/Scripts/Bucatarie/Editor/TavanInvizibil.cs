using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Unsteady.EditorTools
{
    /// Un tavan invizibil (doar collider, fără mesh) deasupra pereților bucătăriei,
    /// ca obiectele aruncate în sus să nu zboare afară din cameră.
    public static class TavanInvizibil
    {
        const string Nume = "Tavan_Invizibil";
        const float Grosime = 0.3f;

        [MenuItem("Unsteady/13. Tavan invizibil (obiectele aruncate rămân în cameră)")]
        static void Aplica()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("Unsteady", "Oprește Play întâi.", "OK");
                return;
            }

            var scena = EditorSceneManager.GetActiveScene();
            var pereti = scena.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Collider>(true))
                .Where(c => c.name.StartsWith("Perete") || c.name == "Podea")
                .ToList();
            if (pereti.Count == 0)
            {
                EditorUtility.DisplayDialog("Unsteady", "Nu găsesc pereții (Perete_*) în scena deschisă.", "OK");
                return;
            }

            Bounds b = pereti[0].bounds;
            foreach (var c in pereti) b.Encapsulate(c.bounds);

            var go = scena.GetRootGameObjects().FirstOrDefault(g => g.name == Nume);
            if (go == null)
            {
                go = new GameObject(Nume);
                Undo.RegisterCreatedObjectUndo(go, "Tavan invizibil");
            }
            go.transform.SetPositionAndRotation(new Vector3(b.center.x, b.max.y + Grosime * 0.5f, b.center.z), Quaternion.identity);
            go.transform.localScale = Vector3.one;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.NavigationStatic);

            var box = go.GetComponent<BoxCollider>();
            if (box == null) box = go.AddComponent<BoxCollider>();
            // puțin mai mare decât camera, ca să nu rămână fante la colțuri
            box.size = new Vector3(b.size.x + 0.4f, Grosime, b.size.z + 0.4f);
            box.center = Vector3.zero;
            box.isTrigger = false;

            EditorSceneManager.MarkSceneDirty(scena);
            EditorSceneManager.SaveScene(scena);
            Selection.activeGameObject = go;

            string msg = $"Tavanul invizibil e pus la {b.max.y:F2} m (cât pereții), {box.size.x:F1} × {box.size.z:F1} m. Obiectele aruncate în sus se lovesc de el și cad înapoi.";
            Debug.Log("[Unsteady] " + msg);
            EditorUtility.DisplayDialog("Unsteady", msg, "OK");
        }
    }
}
