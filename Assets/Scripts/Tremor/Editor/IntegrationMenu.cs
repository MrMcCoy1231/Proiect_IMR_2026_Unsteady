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

        const string PaharPrefabPath = "Assets/Prefabs/Pahar/Pahar.prefab";
        const string PaharTintaPrefabPath = "Assets/Prefabs/Pahar_tinta/Pahar_Tinta.prefab";

        [MenuItem("Unsteady/6. Integrează paharele (Ștefan) în SampleScene")]
        static void IntegrateCups()
        {
            var cup = AssetDatabase.LoadAssetAtPath<GameObject>(PaharPrefabPath);
            var target = AssetDatabase.LoadAssetAtPath<GameObject>(PaharTintaPrefabPath);
            if (cup == null || target == null)
            {
                EditorUtility.DisplayDialog("Unsteady", "Lipsesc prefaburile Pahar / Pahar_Tinta. Ai făcut Pull pe main?", "OK");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // Paharul gol din graybox: îl înlocuim cu prefabul lui Ștefan, în același loc.
            var old = GameObject.Find("Pahar");
            bool already = old != null && PrefabUtility.GetCorrespondingObjectFromSource(old) == cup;
            Vector3 pos = old != null ? old.transform.position : new Vector3(0.277f, 0.8f, 0.092f);
            Transform parent = old != null ? old.transform.parent : null;

            GameObject cupGo = old;
            if (!already)
            {
                cupGo = (GameObject)PrefabUtility.InstantiatePrefab(cup, scene);
                Undo.RegisterCreatedObjectUndo(cupGo, "Pahar Stefan");
                if (parent != null) cupGo.transform.SetParent(parent, true);
                cupGo.transform.position = pos;              // rotația și scara rămân cele din prefab
                if (old != null)
                {
                    cupGo.transform.SetSiblingIndex(old.transform.GetSiblingIndex());
                    Undo.DestroyObjectImmediate(old);
                }
                cupGo.name = "Pahar";
            }

            // Paharul țintă: lângă pahar, ca în Dev_Stefan (16 cm la dreapta).
            if (GameObject.Find("Pahar_Tinta") == null)
            {
                var t = (GameObject)PrefabUtility.InstantiatePrefab(target, scene);
                Undo.RegisterCreatedObjectUndo(t, "Pahar_Tinta Stefan");
                if (cupGo.transform.parent != null) t.transform.SetParent(cupGo.transform.parent, true);
                t.transform.position = cupGo.transform.position + Vector3.right * 0.162f;
                t.transform.SetSiblingIndex(cupGo.transform.GetSiblingIndex() + 1);
                t.name = "Pahar_Tinta";
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = cupGo;
            Debug.Log("[Integrare] Paharele lui Ștefan sunt în SampleScene. SampleScene salvată.");
        }

        const string DecorPrefabPath = "Assets/Prefabs/Bucatarie/Bucatarie_Decor.prefab";

        [MenuItem("Unsteady/9. Integrează bucătăria cu decor (Anca) + lumina în SampleScene")]
        static void IntegrateDecor()
        {
            var decor = AssetDatabase.LoadAssetAtPath<GameObject>(DecorPrefabPath);
            if (decor == null) { EditorUtility.DisplayDialog("Unsteady", "Lipsește Bucatarie_Decor.prefab. Ai făcut Pull pe main?", "OK"); return; }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            // 1) Bucătăria veche (graybox) → prefabul Anca, aliniat exact peste ea.
            GameObject decorGo = null;
            foreach (var root in scene.GetRootGameObjects())
                if (PrefabUtility.GetCorrespondingObjectFromSource(root) == decor) decorGo = root;

            if (decorGo == null)
            {
                var old = GameObject.Find("Bucatarie_Graybox");
                decorGo = (GameObject)PrefabUtility.InstantiatePrefab(decor, scene);
                Undo.RegisterCreatedObjectUndo(decorGo, "Bucatarie_Decor");
                decorGo.transform.position = Vector3.zero;
                if (old != null && old.transform.parent == null)
                {
                    // Graybox-ul din prefab trebuie să ajungă exact unde era cel vechi (masa, tabla, paharele stau pe el).
                    Transform inner = null;
                    foreach (Transform c in decorGo.transform) if (c.name == "Bucatarie_Graybox") inner = c;
                    if (inner != null) decorGo.transform.position += old.transform.position - inner.position;
                    decorGo.transform.SetSiblingIndex(old.transform.GetSiblingIndex());
                    Undo.DestroyObjectImmediate(old);
                }
                decorGo.name = "Bucatarie_Decor";
            }

            // 2) Lumina, după instrucțiunile din PR-ul Anca.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color32(168, 149, 128, 255);
            RenderSettings.reflectionIntensity = 0.3f;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (l.type == LightType.Directional)
                {
                    Undo.RecordObject(l, "Directional Light");
                    l.color = new Color32(255, 228, 194, 255);
                    l.intensity = 0.4f;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(l);
                }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // 3) Generate Lighting (luminile Anca sunt Baked: fără asta nu luminează nimic).
            if (!Lightmapping.BakeAsync())
                Debug.LogWarning("[Integrare] Nu am putut porni Generate Lighting. Pornește-l din Window → Rendering → Lighting.");
            Debug.Log("[Integrare] Bucataria Anca e în SampleScene, lumina e setată, Generate Lighting a pornit (bara din dreapta-jos).");
        }

        [MenuItem("Unsteady/Utilitare/Scoate grab-ul de pe tastatură (SampleScene)")]
        static void RemoveKeyboardGrab()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var kb = GameObject.Find("Tastatura");
            if (kb == null) { Debug.LogWarning("[Integrare] Nu am găsit Tastatura."); return; }

            int scoase = 0;
            // Doar componentele adăugate pe instanța din scenă (nu cele din prefab, nici collider-ele tastelor).
            foreach (var c in kb.GetComponents<Component>())
            {
                if (c == null || c is Transform) continue;
                bool tinta = c is UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable
                          || c is Rigidbody
                          || c is Collider;
                if (!tinta) continue;
                if (PrefabUtility.IsPartOfPrefabInstance(c) && !PrefabUtility.IsAddedComponentOverride(c)) continue;
                Undo.DestroyObjectImmediate(c);
                scoase++;
            }
            // Ordinea contează (grab-ul depinde de Rigidbody): mai trecem o dată pentru ce a rămas.
            foreach (var c in kb.GetComponents<Component>())
            {
                if (c is Rigidbody || c is Collider)
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(c) && !PrefabUtility.IsAddedComponentOverride(c)) continue;
                    Undo.DestroyObjectImmediate(c); scoase++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Integrare] Am scos {scoase} componente de grab de pe Tastatura. SampleScene salvată.");
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
