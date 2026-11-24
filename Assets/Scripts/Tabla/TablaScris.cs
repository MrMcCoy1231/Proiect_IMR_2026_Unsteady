using System;
using System.Collections.Generic;
using TMPro;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;
using Unsteady.Flux;
using Unsteady.Tremor;

namespace Unsteady.Tabla
{
    /// <summary>
    /// Stația 3 din modul Empatie: scrii un cuvânt pe tablă cu markerul.
    /// Când vârful markerului atinge fața tablei, se desenează o linie (LineRenderer).
    /// Pe tablă: cuvântul de scris, două linii de ghidaj (ca la caietul de școală),
    /// butoanele Șterge și Gata. Rezultat: timpul, de câte ori ai ieșit din rânduri și abaterea medie.
    /// Se pune pe Tabla (meniul Unsteady → 8 face totul automat).
    /// </summary>
    public class TablaScris : MonoBehaviour, IStatie
    {
        [Header("Legături (completate de Unsteady → 8)")]
        public Transform marker;
        [Tooltip("Un punct gol pe vârful markerului.")]
        public Transform varf;

        [Header("Scris")]
        public Color culoare = new Color(0.12f, 0.20f, 0.55f);
        public float grosime = 0.005f;
        [Tooltip("Cât de aproape de tablă (m) trebuie să fie vârful ca să înceapă să scrie.")]
        public float distantaContact = 0.012f;
        [Tooltip("Odată pornită, linia continuă până când vârful se depărtează la distanța asta (m). Așa nu se rupe la tremor.")]
        public float distantaDesprindere = 0.03f;
        [Tooltip("Cât timp (s) poate lipsi contactul fără să se rupă linia.")]
        public float pauzaMaxima = 0.08f;
        public string[] cuvinte = { "MAMA", "CASA", "APA", "SOARE", "ANA" };

        [Header("Rânduri (ghidaj)")]
        [Tooltip("Înălțimea rândului în care scrii (m), ca la caiet.")]
        public float inaltimeRand = 0.14f;

        // geometria tablei (world)
        Vector3 centruFata, normala, sus, dreapta;
        float jumLatime, jumInaltime;

        // desen
        readonly List<LineRenderer> linii = new List<LineRenderer>();
        LineRenderer liniaCurenta;
        Vector3 ultimulPunct;
        float timpDesprins;
        Material matLinie;
        Transform containerLinii;

        // UI
        GameObject ui;
        TMP_Text textCuvant, textStare;

        // stare
        bool controlatDeFlux, activ = true;
        int cuvantIdx, ultimaIncercare = -1;
        float startTime;
        int puncte, iesiri; float sumaAbatere; bool afara;

        Vector3 markerPos0; Quaternion markerRot0;
        XRGrabInteractable grabMarker;
        Rigidbody rbMarker;

        public event Action<RezultatStatie> Gata;
        public string Nume => "Scrie pe tablă";
        public string Instructiuni =>
            "Apucă markerul cu <b>grip</b> și scrie pe tablă cuvântul de sus, între cele două linii.\nCând termini, ochește butonul <b>Gata</b> de pe tablă și apasă trigger-ul.";

        string Cuvant => cuvinte.Length > 0 ? cuvinte[cuvantIdx % cuvinte.Length] : "";

        void Start()
        {
            CalculeazaGeometria();
            matLinie = new Material(Shader.Find("Sprites/Default"));
            // Foaia albă (UI) și liniile sunt ambele transparente, iar Unity le sortează după distanța față de cameră:
            // când îți mișcai capul, foaia ajungea desenată peste linii și scrisul „dispărea”.
            // Cu o coadă de randare mai mare, liniile se desenează mereu după foaie (markerul și mâna tot le acoperă).
            matLinie.renderQueue = 3100;
            containerLinii = new GameObject("Scris").transform;
            containerLinii.SetParent(transform, true);
            ConstruiesteUI();

            if (marker != null)
            {
                markerPos0 = marker.position; markerRot0 = marker.rotation;
                grabMarker = marker.GetComponentInChildren<XRGrabInteractable>();
                rbMarker = marker.GetComponentInChildren<Rigidbody>();
            }

            controlatDeFlux = FindFirstObjectByType<EmpatieFlow>() != null;
            if (controlatDeFlux) { activ = false; ui.SetActive(false); }
            ActualizeazaText();
        }

        // ---------------- geometria tablei ----------------

        void CalculeazaGeometria()
        {
            var mf = GetComponentInChildren<MeshFilter>();
            Transform t = mf != null ? mf.transform : transform;
            Bounds b = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds : new Bounds(Vector3.zero, Vector3.one);

            Vector3 centru = t.TransformPoint(b.center);
            var e = new Vector3[3];
            for (int i = 0; i < 3; i++)
            {
                Vector3 ax = Vector3.zero; ax[i] = b.extents[i];
                e[i] = t.TransformVector(ax);           // semi-axele tablei în lume
            }

            int n = 0;                                  // axa cea mai subțire = normala
            for (int i = 1; i < 3; i++) if (e[i].magnitude < e[n].magnitude) n = i;
            normala = e[n].normalized;
            Vector3 cam = Camera.main != null ? Camera.main.transform.position : centru + normala;
            if (Vector3.Dot(cam - centru, normala) < 0) normala = -normala;   // fața dinspre cameră
            centruFata = centru + normala * e[n].magnitude;

            int a1 = (n + 1) % 3, a2 = (n + 2) % 3;     // dintre celelalte două, "sus" e cea mai verticală
            int u = Mathf.Abs(Vector3.Dot(e[a1].normalized, Vector3.up)) >= Mathf.Abs(Vector3.Dot(e[a2].normalized, Vector3.up)) ? a1 : a2;
            int r = u == a1 ? a2 : a1;
            sus = e[u].normalized; if (Vector3.Dot(sus, Vector3.up) < 0) sus = -sus;
            jumInaltime = e[u].magnitude;
            jumLatime = e[r].magnitude;
            dreapta = Vector3.Cross(sus, -normala).normalized;   // dreapta văzută de cel din fața tablei
        }

        // ---------------- desenul ----------------

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (Application.isEditor && Keyboard.current != null && Keyboard.current.f7Key.wasPressedThisFrame && !testInCurs)
                StartCoroutine(TestScris());
#endif
            if (varf == null) return;
            Vector3 rel = varf.position - centruFata;
            float d = Vector3.Dot(rel, normala);
            float x = Vector3.Dot(rel, dreapta);
            float y = Vector3.Dot(rel, sus);

            // Histerezis: începi să scrii aproape de tablă, dar linia se oprește abia când te depărtezi mai mult.
            float prag = liniaCurenta != null ? distantaDesprindere : distantaContact;
            bool atinge = activ && d < prag && d > -0.06f &&
                          Mathf.Abs(x) < jumLatime - 0.01f && Mathf.Abs(y) < jumInaltime - 0.01f;
            if (!atinge)
            {
                if (liniaCurenta != null)
                {
                    timpDesprins += Time.deltaTime;
                    if (timpDesprins > pauzaMaxima) liniaCurenta = null;
                }
                return;
            }
            timpDesprins = 0f;

            Vector3 p = centruFata + dreapta * x + sus * y + normala * 0.006f;   // în fața foii albe
            if (liniaCurenta == null)
            {
                liniaCurenta = LinieNoua();
                Adauga(p, y);
            }
            else if ((p - ultimulPunct).sqrMagnitude > 0.002f * 0.002f)
            {
                Adauga(p, y);
            }
        }

        LineRenderer LinieNoua()
        {
            var go = new GameObject("Linie");
            go.transform.SetParent(containerLinii, true);
            go.transform.rotation = Quaternion.LookRotation(-normala, sus);   // linia stă culcată pe tablă
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.TransformZ;
            lr.material = matLinie;
            lr.startColor = lr.endColor = culoare;
            lr.widthMultiplier = grosime;
            lr.numCapVertices = 3;
            lr.numCornerVertices = 2;
            lr.positionCount = 0;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            linii.Add(lr);
            return lr;
        }

        void Adauga(Vector3 p, float y)
        {
            liniaCurenta.positionCount++;
            liniaCurenta.SetPosition(liniaCurenta.positionCount - 1, p);
            ultimulPunct = p;

            // Măsurătoarea: cât iese scrisul din rândul dintre cele două linii.
            float iesire = Mathf.Max(0f, Mathf.Abs(y - YRand) - inaltimeRand * 0.5f);
            puncte++;
            sumaAbatere += iesire;
            bool acumAfara = iesire > 0.005f;
            if (acumAfara && !afara) iesiri++;
            afara = acumAfara;
        }

        float YRand => -jumInaltime * 0.05f;   // rândul e puțin sub mijlocul tablei

        public void Sterge()
        {
            foreach (var l in linii) if (l != null) Destroy(l.gameObject);
            linii.Clear();
            liniaCurenta = null;
            puncte = 0; iesiri = 0; sumaAbatere = 0; afara = false;
            ActualizeazaText();
        }

        // ---------------- test în Editor (F7) ----------------

        bool testInCurs;

        /// <summary>F7 în Editor: mută markerul ca și cum ar scrie o linie ondulată pe tablă, apoi apasă Gata (în fluxul Empatie).</summary>
        IEnumerator TestScris()
        {
            if (marker == null || varf == null) yield break;
            testInCurs = true;
            bool kin = rbMarker != null && rbMarker.isKinematic;
            if (rbMarker != null) rbMarker.isKinematic = true;
            Vector3 offset = marker.position - varf.position;
            for (float t = 0; t < 2f; t += Time.deltaTime)
            {
                float x = Mathf.Lerp(-jumLatime * 0.6f, jumLatime * 0.6f, t / 2f);
                float y = YRand + Mathf.Sin(t * 9f) * inaltimeRand * 0.7f;
                marker.position = centruFata + dreapta * x + sus * y + offset;
                yield return null;
            }
            marker.position += normala * 0.1f;   // ridică markerul de pe tablă
            yield return null;
            if (rbMarker != null) rbMarker.isKinematic = kin;
            if (textStare) textStare.text = $"Test: {puncte} puncte, {iesiri} ieșiri din rând";
            testInCurs = false;
            if (controlatDeFlux && activ) { yield return new WaitForSeconds(0.8f); ApasaGata(); }   // ca și cum ai apăsa Gata
        }

        // ---------------- UI pe tablă ----------------

        void ConstruiesteUI()
        {
            // Fără părinte: tabla din graybox are scară neuniformă, care ar deforma textul.
            ui = new GameObject("UI_Tabla", typeof(RectTransform));
            var canvas = ui.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            ui.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
            ui.AddComponent<TrackedDeviceGraphicRaycaster>();
            ui.AddComponent<GraphicRaycaster>();

            var rt = (RectTransform)ui.transform;
            rt.position = centruFata + normala * 0.003f;
            rt.rotation = Quaternion.LookRotation(-normala, sus);
            rt.localScale = Vector3.one * 0.001f;                       // 1 unitate = 1 mm
            float W = jumLatime * 2000f, H = jumInaltime * 2000f;
            rt.sizeDelta = new Vector2(W, H);

            // Foaia albă a tablei (whiteboard), ca scrisul să se vadă bine.
            var foaie = new GameObject("Foaie", typeof(RectTransform), typeof(Image));
            foaie.transform.SetParent(rt, false);
            var fr = (RectTransform)foaie.transform;
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = new Vector2(15, 15); fr.offsetMax = new Vector2(-15, -15);
            var fi = foaie.GetComponent<Image>(); fi.color = new Color(0.96f, 0.96f, 0.94f); fi.raycastTarget = false;

            // Cuvântul de scris, sus.
            textCuvant = Text(rt, "Cuvant", "", 70, new Vector2(0, H * 0.5f - 70), new Vector2(W - 60, 110));
            textCuvant.color = new Color(0.15f, 0.15f, 0.18f);

            // Cele două linii de ghidaj ale rândului.
            float yr = YRand * 1000f, h = inaltimeRand * 1000f;
            Bara(rt, "Linie_Sus", yr + h * 0.5f, W - 80);
            Bara(rt, "Linie_Jos", yr - h * 0.5f, W - 80);

            // Butoanele și starea, jos.
            float yb = -H * 0.5f + 55;
            Buton(rt, "Buton_Sterge", "Șterge", new Vector2(-W * 0.5f + 130, yb), new Color(0.30f, 0.33f, 0.38f), Sterge);
            Buton(rt, "Buton_Gata", "Gata", new Vector2(W * 0.5f - 130, yb), new Color(0.95f, 0.55f, 0.20f), ApasaGata);
            textStare = Text(rt, "Stare", "", 30, new Vector2(0, yb), new Vector2(W - 560, 70));
            textStare.color = new Color(0.25f, 0.25f, 0.30f);
        }

        static TextMeshProUGUI Text(RectTransform parent, string name, string text, float size, Vector2 pos, Vector2 sz)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchoredPosition = pos; r.sizeDelta = sz;
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text; t.fontSize = size; t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
            return t;
        }

        static void Bara(RectTransform parent, string name, float y, float w)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchoredPosition = new Vector2(0, y); r.sizeDelta = new Vector2(w, 4);
            var img = go.GetComponent<Image>(); img.color = new Color(0.55f, 0.70f, 0.90f, 0.9f); img.raycastTarget = false;
        }

        static void Buton(RectTransform parent, string name, string label, Vector2 pos, Color color, UnityEngine.Events.UnityAction a)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchoredPosition = pos; r.sizeDelta = new Vector2(220, 80);
            go.GetComponent<Image>().color = color;
            go.GetComponent<Button>().onClick.AddListener(a);
            var t = Text(r, "Text", label, 34, Vector2.zero, new Vector2(220, 80));
            t.color = Color.white; t.fontStyle = FontStyles.Bold;
        }

        void ActualizeazaText()
        {
            if (textCuvant != null) textCuvant.text = $"Scrie: <b>{Cuvant}</b>";
            if (textStare != null && activ) textStare.text = "Scrie între cele două linii";
        }

        // ---------------- IStatie ----------------

        void ApasaGata()
        {
            if (!activ) return;
            float abatereCm = puncte > 0 ? sumaAbatere / puncte * 100f : 0f;
            var r = new RezultatStatie
            {
                timpSecunde = Time.time - startTime,
                greseli = iesiri,
                etichetaGreseli = iesiri == 1 ? "ieșire" : "ieșiri",
                scor = Mathf.Clamp01(1f - abatereCm / 5f),
                detalii = $"abatere medie {abatereCm:0.0} cm"
            };

            if (controlatDeFlux)
            {
                activ = false;
                if (textStare) textStare.text = "Gata!";
                Gata?.Invoke(r);
            }
            else
            {
                var lvl = TremorManager.Instance != null ? TremorManager.Instance.Level : TremorLevel.Oprit;
                if (textStare) textStare.text = $"{r.timpSecunde:0.0} s · {iesiri} ieșiri · {r.detalii} · tremor {TremorManager.LevelName(lvl)}";
                startTime = Time.time;
            }
        }

        public void Pregateste(int incercare)
        {
            if (incercare == 0 && ultimaIncercare != -1 && cuvinte.Length > 0) cuvantIdx = (cuvantIdx + 1) % cuvinte.Length;
            ultimaIncercare = incercare;
            activ = false;
            Sterge();
            PuneMarkerulInapoi();
            ui.SetActive(true);
            if (textStare) textStare.text = "Apasă <b>Start</b> pe panou";
            ActualizeazaText();
        }

        public void Porneste()
        {
            activ = true;
            startTime = Time.time;
            ActualizeazaText();
        }

        public void Opreste()
        {
            activ = false;
            Sterge();
            if (ui != null) ui.SetActive(false);
        }

        void PuneMarkerulInapoi()
        {
            if (marker == null || (grabMarker != null && grabMarker.isSelected)) return;
            marker.SetPositionAndRotation(markerPos0, markerRot0);
            if (rbMarker != null && !rbMarker.isKinematic)
            {
                rbMarker.linearVelocity = Vector3.zero;
                rbMarker.angularVelocity = Vector3.zero;
            }
        }
    }
}
