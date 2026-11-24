using System.Collections.Generic;
using System.Reflection;
using UnityEngine;


/// UN SINGUR script pentru ORICE pahar.
/// Fiecare pahar are un nivel 0..1. Când îl înclini suficient, apa curge:
///  - jetul cade pe o traiectorie reală (parabolă); dacă aterizează în gura altui Pahar, apa trece în acela;
///  - altfel se varsă (se numără în VarsatMl).
/// Jetul se desenează cu un LineRenderer (o „șuviță” de apă care se subțiază pe măsură ce cade),
/// iar sistemul de particule face doar stropii mici de la locul unde lovește apa.
/// Cantitatea de apă se calculează din cod, nu din particule.

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class Pahar : MonoBehaviour
{
    static readonly List<Pahar> toate = new List<Pahar>();

    [Header("Referințe (copii ai paharului)")]
    public Transform apaVizual;          // cilindrul de apă
    public Transform gura;               // empty în centrul deschiderii de sus (se creează singur dacă lipsește)
    public ParticleSystem particule;     // stropii de la impact (doar vizual)

    [Header("Dimensiuni")]
    [Tooltip("Raza deschiderii, în metri. Dacă e bifat 'razaGuraAutomata', se calculează din mesh.")]
    public float razaGura = 0.04f;
    public bool razaGuraAutomata = true;
    public float capacitateMl = 250f;
    [Tooltip("1 pentru Cylinder-ul din Unity (înălțime 2), 0.5 pentru un mesh cu înălțime 1")]
    public float semiInaltimeMesh = 1f;

    [Header("Turnare")]
    [Tooltip("Unghiul de înclinare la care începe să curgă când paharul e PLIN")]
    public float unghiPornirePlin = 50f;
    [Tooltip("Unghiul la care începe să curgă când paharul e aproape GOL")]
    public float unghiPornireGol = 110f;
    [Tooltip("Câte grade după prag până la debit maxim")]
    public float rampaUnghi = 25f;
    [Tooltip("Fracțiuni de pahar pe secundă la debit maxim")]
    public float debitMaxim = 0.4f;
    [Tooltip("Cât de generos e țintitul (1 = exact gura, 1.3 = cu marjă)")]
    public float toleranta = 1.3f;

    [Header("Jetul de apă")]
    [Tooltip("Material cu shader Sprites/Default. Dacă e gol, se creează unul.")]
    public Material materialJet;
    public Color culoareJet = new Color(0.78f, 0.9f, 1f, 0.8f);
    [Tooltip("Grosimea jetului la debit mic / mare, în metri")]
    public float grosimeMin = 0.004f, grosimeMax = 0.011f;
    [Tooltip("Viteza orizontală cu care iese apa la debit mic / mare (m/s)")]
    public float vitezaMin = 0.15f, vitezaMax = 0.45f;

    [Header("Doar pentru scena de test (stația îl setează singură)")]
    [Range(0f, 1f)] public float nivelInitial = 0f;

    public float Nivel => nivel;
    public float VolumMl => nivel * capacitateMl;
    public float VarsatMl => varsatMl;
    public bool Toarna => curge;
    public Vector3 AxaSus => transform.TransformDirection(axaLocala);

    public bool EsteApucat
    {
        get
        {
            if (grab == null || isSelectedProp == null) return false;
            return (bool)isSelectedProp.GetValue(grab);
        }
    }

    float nivel;
    float varsatMl;
    bool curge;

    Rigidbody rb;
    Component grab;
    PropertyInfo isSelectedProp;
    Collider[] colidereProprii;

    Vector3 axaLocala = Vector3.up;
    Vector3 poz0, scala0;
    Vector3 pozitieStart;
    Quaternion rotatieStart;

    Vector3 ultimP0, ultimV0;

    LineRenderer jet;
    Material matJetInstanta;
    float offsetTextura;
    const int PuncteJet = 16;
    readonly Vector3[] puncte = new Vector3[PuncteJet];
    static Texture2D texturaJet;
    static readonly RaycastHit[] loviri = new RaycastHit[8];

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        colidereProprii = GetComponentsInChildren<Collider>(true);

        // căutăm XR Grab Interactable după nume, ca să meargă cu orice versiune de XRI
        grab = GetComponent("XRGrabInteractable");
        if (grab != null) isSelectedProp = grab.GetType().GetProperty("isSelected");
        else Debug.LogWarning($"[Pahar] {name}: nu are XR Grab Interactable (EsteApucat va fi mereu false).", this);

        // axa "în sus" a paharului = axa locală cea mai apropiată de verticală la start
        Vector3[] candidati = { Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        float min = 180f;
        foreach (var c in candidati)
        {
            float u = Vector3.Angle(transform.TransformDirection(c), Vector3.up);
            if (u < min) { min = u; axaLocala = c; }
        }
        if (min > 20f)
            Debug.LogWarning($"[Pahar] {name}: la start paharul nu stă drept (abatere {min:F0} grade). Îndreaptă-l în scenă.", this);

        MasoaraPaharul();

        if (apaVizual != null)
        {
            poz0 = apaVizual.localPosition;
            scala0 = apaVizual.localScale;
        }

        pozitieStart = transform.position;
        rotatieStart = transform.rotation;

        ConfigureazaStropi();
        CreeazaJet();

        nivel = nivelInitial;
        AplicaVizual();
    }

    void OnEnable()
    {
        if (!toate.Contains(this)) toate.Add(this);
    }

    void OnDisable()
    {
        toate.Remove(this);
        curge = false;
        if (jet != null) jet.enabled = false;
        if (particule != null) { var em = particule.emission; em.enabled = false; }
    }

    void OnDestroy()
    {
        if (matJetInstanta != null) Destroy(matJetInstanta);
    }

    void Update()
    {
        Vector3 sus = AxaSus;
        float unghi = Vector3.Angle(sus, Vector3.up);
        float prag = Mathf.Lerp(unghiPornireGol, unghiPornirePlin, nivel);

        float debit = 0f;
        if (nivel > 0.003f && unghi > prag)
            debit = debitMaxim * Mathf.Clamp01((unghi - prag) / Mathf.Max(1f, rampaUnghi));
        float debitNorm = debit / Mathf.Max(0.0001f, debitMaxim);

        Vector3 dir = DirectieTurnare(sus);
        Vector3 p0 = gura.position + dir * razaGura;
        Vector3 v0 = dir * Mathf.Lerp(vitezaMin, vitezaMax, debitNorm) + Vector3.down * 0.05f;
        ultimP0 = p0; ultimV0 = v0;

        Pahar tinta = null;
        if (debit > 0f)
        {
            float cantitate = Mathf.Min(debit * Time.deltaTime, nivel);
            nivel -= cantitate;
            float ml = cantitate * capacitateMl;

            tinta = GasesteTinta(p0, v0);
            if (tinta != null)
            {
                float locMl = (1f - tinta.nivel) * tinta.capacitateMl;
                float primitMl = Mathf.Min(ml, locMl);
                tinta.nivel += primitMl / tinta.capacitateMl;
                varsatMl += ml - primitMl;   // ce nu mai încape se varsă
            }
            else
            {
                varsatMl += ml;              // nimic dedesubt: se varsă
            }
        }

        curge = debit > 0f;
        ActualizeazaJet(debitNorm, p0, v0, tinta);
        AplicaVizual();
    }

    // ---------------- traiectoria ----------------

    // direcția orizontală spre care e înclinat paharul (pe acolo iese apa)
    Vector3 DirectieTurnare(Vector3 sus)
    {
        Vector3 orizontal = Vector3.ProjectOnPlane(sus, Vector3.up);
        if (orizontal.sqrMagnitude < 1e-4f)
            orizontal = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        return orizontal.normalized;
    }

    static Vector3 Pozitie(Vector3 p0, Vector3 v0, float t) => p0 + v0 * t + 0.5f * Physics.gravity * (t * t);

    // momentul la care jetul coboară la înălțimea y (sau -1 dacă nu ajunge acolo)
    static float TimpLaInaltime(Vector3 p0, Vector3 v0, float y)
    {
        float a = 0.5f * Physics.gravity.y, b = v0.y, c = p0.y - y;
        if (Mathf.Abs(a) < 1e-5f) return -1f;
        float disc = b * b - 4f * a * c;
        if (disc < 0f) return -1f;
        float t = (-b - Mathf.Sqrt(disc)) / (2f * a);
        return t > 0f ? t : -1f;
    }

    /// <summary>Unde ar ateriza jetul (la înălțimea y) cu înclinarea de acum. Folosit de testul F9.</summary>
    public bool PrezicereAterizare(float y, out Vector3 punct)
    {
        float t = TimpLaInaltime(ultimP0, ultimV0, y);
        punct = t > 0f ? Pozitie(ultimP0, ultimV0, t) : ultimP0;
        return t > 0f;
    }

    // paharul în a cărui gură aterizează jetul (cel mai apropiat de centru)
    Pahar GasesteTinta(Vector3 p0, Vector3 v0)
    {
        Pahar cel = null;
        float best = float.MaxValue;
        foreach (var p in toate)
        {
            if (p == this || p.gura == null) continue;
            float t = TimpLaInaltime(p0, v0, p.gura.position.y);
            if (t < 0f) continue;                            // trebuie să fim deasupra gurii lui
            Vector3 aterizare = Pozitie(p0, v0, t);
            Vector3 d = p.gura.position - aterizare;
            float h = new Vector2(d.x, d.z).magnitude;
            if (h > p.razaGura * toleranta) continue;
            if (h < best) { best = h; cel = p; }
        }
        return cel;
    }

    // ---------------- partea vizuală a jetului ----------------

    void CreeazaJet()
    {
        var go = new GameObject("Jet apa");
        go.transform.SetParent(transform, false);
        jet = go.AddComponent<LineRenderer>();
        jet.useWorldSpace = true;
        jet.positionCount = PuncteJet;
        jet.numCapVertices = 3;
        jet.numCornerVertices = 2;
        jet.alignment = LineAlignment.View;
        jet.textureMode = LineTextureMode.Stretch;
        jet.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        jet.receiveShadows = false;
        jet.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.35f, 0.75f), new Keyframe(1f, 0.55f));
        jet.startColor = culoareJet;
        jet.endColor = new Color(culoareJet.r, culoareJet.g, culoareJet.b, culoareJet.a * 0.8f);

        Material baza = materialJet;
        if (baza == null)
        {
            var sh = Shader.Find("Sprites/Default");
            if (sh != null) baza = new Material(sh);
        }
        if (baza != null)
        {
            matJetInstanta = new Material(baza);
            matJetInstanta.mainTexture = TexturaJet();
            matJetInstanta.mainTextureScale = new Vector2(2f, 1f);
            jet.sharedMaterial = matJetInstanta;
        }
        jet.enabled = false;
    }

    void ActualizeazaJet(float debitNorm, Vector3 p0, Vector3 v0, Pahar tinta)
    {
        bool activ = debitNorm > 0f;
        if (jet != null && jet.enabled != activ) jet.enabled = activ;
        if (particule != null) { var em = particule.emission; em.enabled = activ; }
        if (!activ || jet == null) return;

        // unde se oprește jetul: la suprafața apei din paharul țintă, sau unde lovește ceva
        float tFinal = -1f;
        Vector3 capat;
        if (tinta != null)
        {
            float ySuprafata = Mathf.Min(tinta.PunctSuprafata(tinta.nivel).y, tinta.gura.position.y);
            tFinal = TimpLaInaltime(p0, v0, ySuprafata);
        }
        if (tFinal < 0f) tFinal = PrimaLovire(p0, v0);
        capat = Pozitie(p0, v0, tFinal);

        float fluturare = 0.0012f * debitNorm;
        for (int i = 0; i < PuncteJet; i++)
        {
            float f = i / (float)(PuncteJet - 1);
            Vector3 p = Pozitie(p0, v0, tFinal * f);
            // o mică ondulare, ca jetul să nu pară o bară rigidă
            if (i > 0 && i < PuncteJet - 1)
                p += new Vector3(Mathf.Sin(Time.time * 37f + i * 1.7f), 0f, Mathf.Cos(Time.time * 31f + i * 2.3f)) * fluturare * f;
            puncte[i] = p;
        }
        puncte[PuncteJet - 1] = capat;
        jet.SetPositions(puncte);
        jet.widthMultiplier = Mathf.Lerp(grosimeMin, grosimeMax, debitNorm);

        if (matJetInstanta != null)
        {
            offsetTextura -= Time.deltaTime * Mathf.Lerp(2.5f, 5f, debitNorm);
            matJetInstanta.mainTextureOffset = new Vector2(offsetTextura, 0f);
        }

        if (particule != null)
        {
            particule.transform.SetPositionAndRotation(capat + Vector3.up * 0.002f, Quaternion.LookRotation(Vector3.up));
            var em = particule.emission;
            em.rateOverTime = Mathf.Lerp(25f, 140f, debitNorm);
            if (!particule.isPlaying) particule.Play();
        }
    }

    // momentul în care jetul lovește prima suprafață (masă, podea), ignorând paharul propriu
    float PrimaLovire(Vector3 p0, Vector3 v0)
    {
        const float pas = 0.025f, maxT = 0.9f;
        Vector3 a = p0;
        for (float t = pas; t <= maxT; t += pas)
        {
            Vector3 b = Pozitie(p0, v0, t);
            Vector3 d = b - a;
            int n = Physics.RaycastNonAlloc(a, d.normalized, loviri, d.magnitude, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue; bool gasit = false;
            for (int i = 0; i < n; i++)
            {
                if (EsteProprie(loviri[i].collider)) continue;
                if (loviri[i].distance < best) { best = loviri[i].distance; gasit = true; }
            }
            if (gasit) return t - pas + pas * (best / Mathf.Max(1e-5f, d.magnitude));
            a = b;
        }
        return maxT;
    }

    bool EsteProprie(Collider c)
    {
        if (c == null) return true;
        if (colidereProprii != null)
            foreach (var x in colidereProprii) if (x == c) return true;
        // nici mâinile / controllerele nu opresc jetul vizual
        return c.attachedRigidbody != null && c.attachedRigidbody.isKinematic && c.GetComponentInParent<Pahar>() == null;
    }

    // textură mică generată din cod: margini transparente, un luciu pe o parte și dungi care „curg”
    static Texture2D TexturaJet()
    {
        if (texturaJet != null) return texturaJet;
        const int W = 64, H = 16;
        texturaJet = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, name = "Jet apa (generat)" };
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
        {
            float v = (y + 0.5f) / H;                                  // de-a latul jetului
            float corp = Mathf.Sin(v * Mathf.PI);                       // gros la mijloc, subțire pe margini
            float luciu = Mathf.Exp(-Mathf.Pow((v - 0.32f) / 0.09f, 2f)); // dunga de lumină
            for (int x = 0; x < W; x++)
            {
                float u = (x + 0.5f) / W;
                float dungi = Mathf.PerlinNoise(u * 6f, v * 3f + 7.3f);   // variații pe lungime
                float lum = Mathf.Clamp01(0.72f + 0.18f * dungi + 0.45f * luciu);
                float alfa = Mathf.Clamp01(Mathf.Pow(corp, 0.6f) * (0.55f + 0.3f * dungi) + 0.35f * luciu);
                px[y * W + x] = new Color(lum, lum, lum, alfa);
            }
        }
        texturaJet.SetPixels(px);
        texturaJet.Apply(false, true);
        return texturaJet;
    }

    // stropii: particule mici, scurte, care sar în sus de unde lovește apa
    void ConfigureazaStropi()
    {
        if (particule == null) return;
        particule.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = particule.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Shape;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.7f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.0025f, 0.006f);
        main.startColor = new Color(0.82f, 0.92f, 1f, 0.75f);
        main.gravityModifier = 1f;
        main.maxParticles = 150;

        var shape = particule.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 40f;
        shape.radius = 0.003f;

        var sol = particule.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f)));

        var col = particule.collision; col.enabled = false;
        var noise = particule.noise; noise.enabled = false;
        var trails = particule.trails; trails.enabled = false;
        var vel = particule.velocityOverLifetime; vel.enabled = false;
        var force = particule.forceOverLifetime; force.enabled = false;

        var em = particule.emission;
        em.enabled = false;
    }

    // ---------------- nivelul de apă ----------------

    void AplicaVizual()
    {
        if (apaVizual == null) return;

        float f = Mathf.Clamp01(nivel);
        bool vizibila = f > 0.003f;
        if (apaVizual.gameObject.activeSelf != vizibila) apaVizual.gameObject.SetActive(vizibila);
        if (!vizibila) return;

        Vector3 s = scala0;
        s.y = scala0.y * f;
        apaVizual.localScale = s;

        // cilindrul crește de jos în sus: ținem fundul pe loc
        Vector3 axa = apaVizual.localRotation * Vector3.up;
        apaVizual.localPosition = poz0 - axa * (scala0.y * semiInaltimeMesh * (1f - f));
    }

    // găsește gura (sau o creează în vârful mesh-ului) și măsoară raza deschiderii
    void MasoaraPaharul()
    {
        bool am = false;
        Bounds b = default;
        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
            if (apaVizual != null && r.transform.IsChildOf(apaVizual)) continue;
            if (!am) { b = r.bounds; am = true; } else b.Encapsulate(r.bounds);
        }

        if (gura == null)
        {
            var g = new GameObject("gura").transform;
            g.SetParent(transform, false);
            g.position = am ? new Vector3(b.center.x, b.max.y, b.center.z) : transform.position;
            gura = g;
        }

        if (razaGuraAutomata && am)
        {
            float ext = Mathf.Min(b.extents.x, b.extents.z);
            if (ext > 0.01f && ext < 0.3f) razaGura = ext * 0.9f;
        }
    }

    /// <summary>Poziția în lume a suprafeței apei la fracțiunea f (0..1). Folosită pentru inel.</summary>
    public Vector3 PunctSuprafata(float f)
    {
        if (apaVizual == null || apaVizual.parent == null) return transform.position;
        Vector3 axa = apaVizual.localRotation * Vector3.up;
        float jumatate = scala0.y * semiInaltimeMesh;
        Vector3 jos = poz0 - axa * jumatate;
        Vector3 local = jos + axa * (2f * jumatate * Mathf.Clamp01(f));
        return apaVizual.parent.TransformPoint(local);
    }

    /// <summary>Inversul lui PunctSuprafata: la ce fracțiune (0..1) din înălțimea paharului se află un punct din lume.</summary>
    public float FractiuneLaPunct(Vector3 punctLume)
    {
        if (apaVizual == null || apaVizual.parent == null) return 0f;
        Vector3 axa = apaVizual.localRotation * Vector3.up;
        float jumatate = scala0.y * semiInaltimeMesh;
        Vector3 jos = poz0 - axa * jumatate;
        Vector3 local = apaVizual.parent.InverseTransformPoint(punctLume);
        return Mathf.Clamp01(Vector3.Dot(local - jos, axa) / (2f * jumatate));
    }

    /// <summary>Reumple/golește, pune paharul înapoi la locul de start, șterge contorul de apă vărsată.</summary>
    public void Reseteaza(float nivelNou)
    {
        nivel = Mathf.Clamp01(nivelNou);
        varsatMl = 0f;
        curge = false;

        if (jet != null) jet.enabled = false;
        if (particule != null)
        {
            var em = particule.emission;
            em.enabled = false;
            particule.Clear();
        }

        if (!EsteApucat)
        {
            transform.SetPositionAndRotation(pozitieStart, rotatieStart);
            if (rb != null && !rb.isKinematic)
            {
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = Vector3.zero;
#else
                rb.velocity = Vector3.zero;
#endif
                rb.angularVelocity = Vector3.zero;
            }
        }

        AplicaVizual();
    }

    void OnDrawGizmosSelected()
    {
        if (gura == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(gura.position, razaGura);
    }
}
