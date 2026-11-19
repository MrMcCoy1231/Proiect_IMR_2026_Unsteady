using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Unsteady.Flux
{
    /// <summary>Trecere prin negru în fața ochilor (merge și în cască).</summary>
    public class ScreenFade : MonoBehaviour
    {
        CanvasGroup grup;

        public static ScreenFade Creeaza(Camera cam)
        {
            var go = new GameObject("Fade", typeof(RectTransform));
            go.transform.SetParent(cam.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, 0.25f);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 32000;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(3000f, 3000f);   // 3 m x 3 m la 25 cm de ochi: acoperă tot
            rt.localScale = Vector3.one * 0.001f;

            var img = new GameObject("Negru", typeof(RectTransform), typeof(Image));
            img.transform.SetParent(go.transform, false);
            var irt = (RectTransform)img.transform;
            irt.anchorMin = Vector2.zero; irt.anchorMax = Vector2.one;
            irt.offsetMin = irt.offsetMax = Vector2.zero;
            var image = img.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = false;

            var f = go.AddComponent<ScreenFade>();
            f.grup = go.AddComponent<CanvasGroup>();
            f.grup.alpha = 0f;
            f.grup.interactable = false;
            f.grup.blocksRaycasts = false;
            return f;
        }

        public IEnumerator La(float tinta, float durata)
        {
            float start = grup.alpha, t = 0f;
            while (t < durata)
            {
                t += Time.deltaTime;
                grup.alpha = Mathf.Lerp(start, tinta, durata > 0 ? t / durata : 1f);
                yield return null;
            }
            grup.alpha = tinta;
        }
    }
}
