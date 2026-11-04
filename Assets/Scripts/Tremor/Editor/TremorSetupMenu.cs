using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unsteady.Tremor;

namespace Unsteady.Tremor.EditorTools
{
    static class TremorSetupMenu
    {
        [MenuItem("Unsteady/1. Adaugă tremorul în scena deschisă")]
        static void AddTremorToScene()
        {
            var manager = Object.FindFirstObjectByType<TremorManager>(FindObjectsInactive.Include);
            if (manager == null)
            {
                var go = new GameObject("Tremor Manager");
                Undo.RegisterCreatedObjectUndo(go, "Creează Tremor Manager");
                manager = go.AddComponent<TremorManager>();
            }

            int added = 0, found = 0;
            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var t in all)
            {
                bool left = t.name == "Left Controller";
                bool right = t.name == "Right Controller";
                if (!left && !right) continue;
                found++;
                if (t.GetComponent<TremorHand>() != null) continue;

                var hand = Undo.AddComponent<TremorHand>(t.gameObject);
                hand.side = left ? TremorHand.Side.Left : TremorHand.Side.Right;
                EditorUtility.SetDirty(hand);
                PrefabUtility.RecordPrefabInstancePropertyModifications(hand);
                added++;
            }

            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            Selection.activeObject = manager.gameObject;

            if (found == 0)
                EditorUtility.DisplayDialog("Unsteady",
                    "Nu am găsit 'Left Controller' / 'Right Controller' în scenă. Există un XR Origin în scena asta?", "OK");
            else
                Debug.Log($"[Tremor] Gata: Tremor Manager în scenă, TremorHand adăugat pe {added} controller(e) " +
                          $"({found} găsite). Salvează scena cu Ctrl+S.");
        }
    }
}
