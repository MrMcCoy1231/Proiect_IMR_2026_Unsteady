using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unsteady.EditorTools
{
    /// Paharele din sticlă: material transparent lucios pentru pahar, apă albăstruie pentru interior
    /// și o textură rotundă pentru stropi. Fără texturi descărcate: totul e generat aici (nimic de declarat ca sursă).
    public static class MaterialePahar
    {
        const string PaharPrefab = "Assets/Prefabs/Pahar/Pahar.prefab";
        const string MatSticla = "Assets/Materiale/Sticla_Pahar.mat";
        const string MatApa = "Assets/Materiale/Apa_Pahar.mat";
        const string MatStropi = "Assets/Materiale/Stropi_Apa.mat";
        const string TexStrop = "Assets/Materiale/Texturi/Strop.png";

        [MenuItem("Unsteady/11. Paharele din sticlă (materiale)")]
        public static void Aplica()
        {
            var sticla = Lit(MatSticla, new Color(0.9f, 0.95f, 1f, 0.14f), 0.9f, 3010, CullMode.Back, false);
            var apa = Lit(MatApa, new Color(0.42f, 0.68f, 0.93f, 0.72f), 0.92f, 3000, CullMode.Back, false);
            var stropi = Particule(MatStropi, TexturaStrop());
            if (sticla == null || apa == null || stropi == null)
            {
                EditorUtility.DisplayDialog("Unsteady", "Nu găsesc shaderele URP (Lit / Particles Unlit).", "OK");
                return;
            }

            NetezesteModelul();

            var root = PrefabUtility.LoadPrefabContents(PaharPrefab);
            try
            {
                var p = root.GetComponent<Pahar>();
                Transform apaT = p != null ? p.apaVizual : null;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r is ParticleSystemRenderer psr) { psr.sharedMaterial = stropi; continue; }
                    if (r is LineRenderer) continue;
                    bool eApa = apaT != null && r.transform.IsChildOf(apaT);
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++) mats[i] = eApa ? apa : sticla;
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
                PrefabUtility.SaveAsPrefabAsset(root, PaharPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            AssetDatabase.SaveAssets();
            EditorUtility.DisplayDialog("Unsteady", "Paharele sunt acum din sticlă, cu apă albăstruie și stropi rotunzi.", "OK");
        }

        // modelul are fețe plate (dungi verticale pe sticlă): calculăm normale netede pe laterale
        static void NetezesteModelul()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PaharPrefab);
            var mf = prefab != null ? prefab.GetComponentInChildren<MeshFilter>(true) : null;
            string cale = mf != null && mf.sharedMesh != null ? AssetDatabase.GetAssetPath(mf.sharedMesh) : null;
            if (string.IsNullOrEmpty(cale) || !cale.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase)) cale = "Assets/Models/Graybox/Pahar.fbx";
            if (!(AssetImporter.GetAtPath(cale) is ModelImporter imp)) { Debug.LogWarning("[Unsteady] Nu găsesc modelul paharului: " + cale); return; }
            if (imp.importNormals == ModelImporterNormals.Calculate && Mathf.Approximately(imp.normalSmoothingAngle, 60f)) return;
            imp.importNormals = ModelImporterNormals.Calculate;
            imp.normalSmoothingAngle = 60f;
            imp.SaveAndReimport();
        }

        // URP Lit transparent, lucios (sticlă / apă)
        static Material Lit(string cale, Color c, float luciu, int coada, CullMode cull, bool reflexii)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh == null) return null;
            var m = AssetDatabase.LoadAssetAtPath<Material>(cale);
            bool nou = m == null;
            if (nou) m = new Material(sh); else m.shader = sh;

            m.SetFloat("_Surface", 1f);            // Transparent
            m.SetFloat("_Blend", 0f);              // Alpha
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", (float)cull);
            m.SetFloat("_AlphaClip", 0f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Smoothness", luciu);
            m.SetFloat("_SpecularHighlights", 1f);
            // fără reflexiile camerei (bucătăria caldă făcea sticla portocalie); rămâne doar luciul luminilor
            m.SetFloat("_EnvironmentReflections", reflexii ? 1f : 0f);
            if (reflexii) m.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF"); else m.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            m.SetFloat("_BlendModePreserveSpecular", 0f);
            m.DisableKeyword("_ALPHAMODULATE_ON");
            m.SetColor("_BaseColor", c);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetShaderPassEnabled("ShadowCaster", false);
            m.renderQueue = coada;                 // apa (3000) se desenează înaintea sticlei (3010)

            if (nou) AssetDatabase.CreateAsset(m, cale); else EditorUtility.SetDirty(m);
            return m;
        }

        // URP Particles Unlit, transparent, cu textura rotundă
        static Material Particule(string cale, Texture2D tex)
        {
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) return null;
            var m = AssetDatabase.LoadAssetAtPath<Material>(cale);
            bool nou = m == null;
            if (nou) m = new Material(sh); else m.shader = sh;

            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.renderQueue = 3020;

            if (nou) AssetDatabase.CreateAsset(m, cale); else EditorUtility.SetDirty(m);
            return m;
        }

        // un punct moale, cu un mic luciu, salvat ca PNG în proiect
        static Texture2D TexturaStrop()
        {
            if (!File.Exists(TexStrop))
            {
                const int N = 64;
                var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
                for (int y = 0; y < N; y++)
                    for (int x = 0; x < N; x++)
                    {
                        float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                        float d = Mathf.Sqrt(dx * dx + dy * dy);
                        float a = Mathf.Clamp01((1f - d) * 3f);
                        float luciu = Mathf.Exp(-((dx + 0.3f) * (dx + 0.3f) + (dy - 0.3f) * (dy - 0.3f)) / 0.04f);
                        float l = Mathf.Clamp01(0.8f + 0.4f * luciu);
                        t.SetPixel(x, y, new Color(l, l, l, a * 0.9f));
                    }
                t.Apply();
                Directory.CreateDirectory(Path.GetDirectoryName(TexStrop));
                File.WriteAllBytes(TexStrop, t.EncodeToPNG());
                Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(TexStrop);
                var imp = (TextureImporter)AssetImporter.GetAtPath(TexStrop);
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexStrop);
        }
    }
}
