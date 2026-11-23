using System.Collections.Generic;
using System.Reflection;
using UnityEngine;


/// UN SINGUR script pentru ORICE pahar.
/// Fiecare pahar are un nivel 0..1. Cand il inclini suficient, apa curge:
///  - daca gura lui e deasupra gurii altui Pahar, apa trece in acela (gasit automat);
///  - altfel se varsa (se numara in VarsatMl).
/// Particulele sunt doar vizuale. Cantitatea de apa se calculeaza din cod,
/// deci nu mai depinde de OnParticleCollision.

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class Pahar : MonoBehaviour
{
    static readonly List<Pahar> toate = new List<Pahar>();

    [Header("Referinte (copii ai paharului)")]
    public Transform apaVizual;          // cilindrul de apa
    public Transform gura;               // empty in centrul deschiderii de sus
    public ParticleSystem particule;     // jetul de apa (doar vizual)

    [Header("Dimensiuni")]
    [Tooltip("Raza deschiderii, in metri (vezi sfera cyan in Scene view)")]
    public float razaGura = 0.04f;
    public float capacitateMl = 250f;
    [Tooltip("1 pentru Cylinder-ul din Unity (inaltime 2), 0.5 pentru un mesh cu inaltime 1")]
    public float semiInaltimeMesh = 1f;

    [Header("Turnare")]
    [Tooltip("Unghiul de inclinare la care incepe sa curga cand paharul e PLIN")]
    public float unghiPornirePlin = 50f;
    [Tooltip("Unghiul la care incepe sa curga cand paharul e aproape GOL")]
    public float unghiPornireGol = 110f;
    [Tooltip("Cate grade dupa prag pana la debit maxim")]
    public float rampaUnghi = 25f;
    [Tooltip("Fractiuni de pahar pe secunda la debit maxim")]
    public float debitMaxim = 0.4f;
    [Tooltip("Cat de generos e tintitul (1 = exact gura, 1.3 = cu marja)")]
    public float toleranta = 1.3f;

    [Header("Doar pentru scena de test (statia il seteaza singura)")]
    [Range(0f, 1f)] public float nivelInitial = 0f;

    public float Nivel => nivel;
    public float VolumMl => nivel * capacitateMl;
    public float VarsatMl => varsatMl;
    public bool Toarna => curge;

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

    Vector3 axaLocala = Vector3.up;
    Vector3 poz0, scala0;
    Vector3 pozitieStart;
    Quaternion rotatieStart;

    Vector3 AxaSusLume => transform.TransformDirection(axaLocala);

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        // cautam XR Grab Interactable dupa nume, ca sa mearga cu orice versiune de XRI
        grab = GetComponent("XRGrabInteractable");
        if (grab != null) isSelectedProp = grab.GetType().GetProperty("isSelected");
        else Debug.LogWarning($"[Pahar] {name}: nu are XR Grab Interactable (EsteApucat va fi mereu false).", this);

        if (gura == null)
        {
            Debug.LogWarning($"[Pahar] {name}: lipseste 'gura'. Folosesc centrul paharului.", this);
            gura = transform;
        }

        // axa "in sus" a paharului = axa locala cea mai apropiata de verticala la start
        Vector3[] candidati = { Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        float min = 180f;
        foreach (var c in candidati)
        {
            float u = Vector3.Angle(transform.TransformDirection(c), Vector3.up);
            if (u < min) { min = u; axaLocala = c; }
        }
        if (min > 20f)
            Debug.LogWarning($"[Pahar] {name}: la start paharul nu sta drept (abatere {min:F0} grade). Indreapta-l in scena.", this);

        if (apaVizual != null)
        {
            poz0 = apaVizual.localPosition;
            scala0 = apaVizual.localScale;
        }

        pozitieStart = transform.position;
        rotatieStart = transform.rotation;

        if (particule != null)
        {
            particule.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var em = particule.emission;
            em.enabled = false;
        }

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
    }

    void Update()
    {
        Vector3 sus = AxaSusLume;
        float unghi = Vector3.Angle(sus, Vector3.up);
        float prag = Mathf.Lerp(unghiPornireGol, unghiPornirePlin, nivel);

        float debit = 0f;
        if (nivel > 0.003f && unghi > prag)
            debit = debitMaxim * Mathf.Clamp01((unghi - prag) / Mathf.Max(1f, rampaUnghi));

        Vector3 punct = PunctTurnare(sus);

        if (debit > 0f)
        {
            float cantitate = Mathf.Min(debit * Time.deltaTime, nivel);
            nivel -= cantitate;
            float ml = cantitate * capacitateMl;

            Pahar tinta = GasesteTinta(punct);
            if (tinta != null)
            {
                float locMl = (1f - tinta.nivel) * tinta.capacitateMl;
                float primitMl = Mathf.Min(ml, locMl);
                tinta.nivel += primitMl / tinta.capacitateMl;
                varsatMl += ml - primitMl;   // ce nu mai incape se varsa
            }
            else
            {
                varsatMl += ml;              // nimic dedesubt: se varsa
            }
        }

        curge = debit > 0f;
        ActualizeazaParticule(debit, punct);
        AplicaVizual();
    }

    // punctul de pe marginea gurii pe unde iese apa (partea spre care e inclinat paharul)
    Vector3 PunctTurnare(Vector3 sus)
    {
        Vector3 orizontal = Vector3.ProjectOnPlane(sus, Vector3.up);
        if (orizontal.sqrMagnitude < 1e-4f)
            orizontal = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        orizontal = orizontal.normalized;
        return gura.position + orizontal * razaGura;
    }

    // alege singur paharul in care se toarna: cel aflat sub punctul de turnare, cu gura in raza
    Pahar GasesteTinta(Vector3 punct)
    {
        Pahar cel = null;
        float best = float.MaxValue;
        foreach (var p in toate)
        {
            if (p == this || p.gura == null) continue;
            Vector3 d = p.gura.position - punct;
            float h = new Vector2(d.x, d.z).magnitude;
            if (h > p.razaGura * toleranta) continue;
            if (punct.y < p.gura.position.y) continue;   // trebuie sa fim deasupra gurii lui
            if (h < best) { best = h; cel = p; }
        }
        return cel;
    }

    void ActualizeazaParticule(float debit, Vector3 punct)
    {
        if (particule == null) return;
        var em = particule.emission;
        bool activ = debit > 0f;
        em.enabled = activ;
        if (!activ) return;

        particule.transform.SetPositionAndRotation(punct, Quaternion.LookRotation(Vector3.down, Vector3.forward));
        em.rateOverTime = Mathf.Lerp(15f, 100f, debit / Mathf.Max(0.0001f, debitMaxim));
        if (!particule.isPlaying) particule.Play();
    }

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

        // cilindrul creste de jos in sus: tinem fundul pe loc
        Vector3 axa = apaVizual.localRotation * Vector3.up;
        apaVizual.localPosition = poz0 - axa * (scala0.y * semiInaltimeMesh * (1f - f));
    }

    /// <summary>Pozitia in lume a suprafetei apei la fractiunea f (0..1). Folosita pentru semnul de umplere.</summary>
    public Vector3 PunctSuprafata(float f)
    {
        if (apaVizual == null || apaVizual.parent == null) return transform.position;
        Vector3 axa = apaVizual.localRotation * Vector3.up;
        float jumatate = scala0.y * semiInaltimeMesh;
        Vector3 jos = poz0 - axa * jumatate;
        Vector3 local = jos + axa * (2f * jumatate * Mathf.Clamp01(f));
        return apaVizual.parent.TransformPoint(local);
    }

    /// <summary>Inversul lui PunctSuprafata: la ce fractiune (0..1) din inaltimea paharului se afla un punct din lume.</summary>
    public float FractiuneLaPunct(Vector3 punctLume)
    {
        if (apaVizual == null || apaVizual.parent == null) return 0f;
        Vector3 axa = apaVizual.localRotation * Vector3.up;
        float jumatate = scala0.y * semiInaltimeMesh;
        Vector3 jos = poz0 - axa * jumatate;
        Vector3 local = apaVizual.parent.InverseTransformPoint(punctLume);
        return Mathf.Clamp01(Vector3.Dot(local - jos, axa) / (2f * jumatate));
    }

    /// <summary>Reumple/goleste, pune paharul inapoi la locul de start, sterge contorul de apa varsata.</summary>
    public void Reseteaza(float nivelNou)
    {
        nivel = Mathf.Clamp01(nivelNou);
        varsatMl = 0f;
        curge = false;

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
