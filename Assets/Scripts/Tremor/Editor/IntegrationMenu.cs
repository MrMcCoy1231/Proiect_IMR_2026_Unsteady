using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Unsteady.Tremor.EditorTools
{
    /// <summary>
    /// Integrarea în SampleScene a muncii colegilor (făcută de Tudor):
    /// materialele lui Anca pe bucătărie și tastatura ei cu taste poke.
    /// </summary>
    static class IntegrationMenu
    {
        const string ScenePath = "Assets/Scenes/SampleScene.unity";
        const string WallMatPath = "Assets/Materiale/Perete_AlbCald.mat";
        const string WoodMatPath = "Assets/Materiale/Mobila_Stejar.mat";
        const string KeyboardPrefabPath = "Assets/Prefabs/Tastatura/Tastatura.prefab";
        const string KitchenPrefabPath = "Assets/Prefabs/Bucatarie/Bucatarie_Graybox.prefab";

        [MenuItem("Unsteady/4. Integrează tastatura + materialele în SampleScene")]
        static void Integrate()
        {
            var wall = AssetDatabase.LoadAssetAtPath<Material>(WallMatPath);
            var wood = AssetDatabase.LoadAssetAtPath<Material>(WoodMatPath);
            var keyboardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(KeyboardPrefabPath);
            if (wall == null || wood == null || keyboardPrefab == null)
            {
                EditorUtility.DisplayDialog("Unsteady", "Lipsesc materialele sau prefabul Tastatura. Ai făcut Pull pe main?", "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            // 1) Repară Perete_E în prefabul de bucătărie al Anca.
            FixKitchenPrefab(wall, wood);

            // 2) SampleScene: materiale pe bucătărie.
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var kitchen = GameObject.Find("Bucatarie_Graybox");
            int painted = 0;
            if (kitchen != null)
            {
                foreach (var r in kitchen.GetComponentsInChildren<Renderer>(true))
                    if (Paint(r, wall, wood)) painted++;
            }
            else
            {
                Debug.LogWarning("[Integrare] Nu am găsit Bucatarie_Graybox în SampleScene.");
            }

            // 3) SampleScene: înlocuiește tastatura goală cu prefabul Anca, în același loc.
            string keyboardMsg = "tastatura era deja integrată";
            var oldKeyboard = GameObject.Find("Tastatura");
            bool alreadyPrefab = oldKeyboard != null &&
                PrefabUtility.GetCorrespondingObjectFromSource(oldKeyboard) == keyboardPrefab;
            if (!alreadyPrefab)
            {
                var kb = (GameObject)PrefabUtility.InstantiatePrefab(keyboardPrefab, scene);
                Undo.RegisterCreatedObjectUndo(kb, "Tastatura Anca");
                if (oldKeyboard != null)
                {
                    kb.transform.SetParent(oldKeyboard.transform.parent, false);
                    kb.transform.SetSiblingIndex(oldKeyboard.transform.GetSiblingIndex());
                    kb.transform.localPosition = oldKeyboard.transform.localPosition;
                    kb.transform.localRotation = oldKeyboard.transform.localRotation;
                    kb.transform.localScale = oldKeyboard.transform.localScale;
                    Undo.DestroyObjectImmediate(oldKeyboard);
                    keyboardMsg = "tastatura înlocuită cu prefabul Anca, în același loc";
                }
                else keyboardMsg = "tastatura adăugată (nu exista una veche, verifică poziția)";
                kb.name = "Tastatura";
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Integrare] Gata: {painted} obiecte colorate, {keyboardMsg}. SampleScene salvată.");
        }

        // Pereți și podea: alb cald. Mobila (masă, scaun, dulapuri, blat): stejar.
        static bool Paint(Renderer r, Material wall, Material wood)
        {
            string n = r.gameObject.name;
            Material m = null;
            if (n.StartsWith("Perete") || n.StartsWith("Podea")) m = wall;
            else if (n.StartsWith("Masa") || n.StartsWith("Scaun") || n.StartsWith("Dulap") || n.StartsWith("Blat")) m = wood;
            if (m == null) return false;

            Undo.RecordObject(r, "Material");
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = m;
            r.sharedMaterials = mats;
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            return true;
        }

        static void FixKitchenPrefab(Material wall, Material wood)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(KitchenPrefabPath) == null) return;
            var root = PrefabUtility.LoadPrefabContents(KitchenPrefabPath);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) Paint(r, wall, wood);
            PrefabUtility.SaveAsPrefabAsset(root, KitchenPrefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
