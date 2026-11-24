using UnityEngine;

/// <summary>
/// Inelul de pe paharul țintă. Urcă odată cu nivelul apei și se face VERDE
/// când apa e în zona țintă a stației (StatieTurnare.nivelSemn ± margineEroare).
/// Pune-l pe obiectul SemnNivel, împreună cu un LineRenderer.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class InelSemn : MonoBehaviour
{
    [Header("Referințe")]
    [Tooltip("Paharul a cărui apă o urmărește inelul. Dacă e lăsat gol, se caută automat.")]
    public Pahar pahar;
    [Tooltip("Stația care spune care e nivelul țintă. Dacă e lăsat gol, se caută automat.")]
    public StatieTurnare statie;
    [Tooltip("Material cu shader Sprites/Default (folosește culorile liniei). Dacă e gol, se creează unul.")]
    public Material materialInel;

    [Header("Dimensiuni")]
    [Tooltip("Raza inelului în metri. Dacă e bifat 'razaAutomata', se calculează din paharul real.")]
    public float raza = 0.045f;
    public bool razaAutomata = true;
    public float grosime = 0.003f;
    public int segmente = 40;

    [Header("Culori")]
    public Color culoareNormala = new Color(1f, 1f, 1f, 0.85f);
    public Color culoareTinta = new Color(0.2f, 0.9f, 0.35f, 1f);
    [Tooltip("Cât de repede trece dintr-o culoare în alta")]
    public float vitezaCuloare = 12f;

    [Header("Comportament")]
    [Tooltip("Dacă e bifat, inelul se ascunde când paharul nu are apă")]
    public bool ascundeCandEGol = false;

    LineRenderer lr;
    Color culoare;
    bool razaCalculata;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.positionCount = Mathf.Max(8, segmente);
        lr.widthMultiplier = grosime;
        lr.numCornerVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;

        if (materialInel != null) lr.sharedMaterial = materialInel;
        else if (lr.sharedMaterial == null || lr.sharedMaterial.shader == null || lr.sharedMaterial.shader.name != "Sprites/Default")
        {
            var sh = Shader.Find("Sprites/Default");
            if (sh != null) lr.material = new Material(sh);
        }

        culoare = culoareNormala;
        AplicaCuloare();
        GasesteReferinte();
    }

    void GasesteReferinte()
    {
        if (statie == null)
        {
#if UNITY_2023_1_OR_NEWER
            statie = FindAnyObjectByType<StatieTurnare>();
#else
            statie = FindObjectOfType<StatieTurnare>();
#endif
        }
        if (pahar == null) pahar = GetComponentInParent<Pahar>();
        if (pahar == null && statie != null) pahar = statie.paharGol;
    }

    void LateUpdate()
    {
        if (pahar == null || statie == null)
        {
            GasesteReferinte();
            if (pahar == null) return;
        }

        if (razaAutomata && !razaCalculata) CalculeazaRaza();

        bool areApa = pahar.Nivel > 0.003f;
        if (ascundeCandEGol)
        {
            if (lr.enabled != areApa) lr.enabled = areApa;
            if (!areApa) return;
        }

        // poziția: suprafața apei; orientarea: axa paharului (inelul se înclină odată cu paharul)
        Vector3 centru = pahar.PunctSuprafata(pahar.Nivel);
        Vector3 sus = pahar.AxaSus;
        Vector3 a = Vector3.Cross(sus, Vector3.forward);
        if (a.sqrMagnitude < 1e-4f) a = Vector3.Cross(sus, Vector3.right);
        a.Normalize();
        Vector3 b = Vector3.Cross(sus, a);

        transform.SetPositionAndRotation(centru, Quaternion.LookRotation(b, sus));
        int n = lr.positionCount;
        for (int i = 0; i < n; i++)
        {
            float u = i / (float)n * Mathf.PI * 2f;
            lr.SetPosition(i, centru + (a * Mathf.Cos(u) + b * Mathf.Sin(u)) * raza);
        }

        // culoarea: verde în zona țintă
        bool laTinta = statie != null && areApa && statie.InZonaTinta(pahar.Nivel);
        Color dorita = laTinta ? culoareTinta : culoareNormala;
        culoare = Color.Lerp(culoare, dorita, 1f - Mathf.Exp(-vitezaCuloare * Time.deltaTime));
        lr.widthMultiplier = laTinta ? grosime * 1.6f : grosime;
        AplicaCuloare();
    }

    void AplicaCuloare()
    {
        if (lr == null) return;
        lr.startColor = culoare;
        lr.endColor = culoare;
    }

    // raza = puțin mai mare decât paharul, măsurată din mesh-ul lui (când stă drept)
    void CalculeazaRaza()
    {
        if (Vector3.Angle(pahar.AxaSus, Vector3.up) > 10f) return;

        bool am = false;
        Bounds b = default;
        foreach (var r in pahar.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
            if (pahar.apaVizual != null && r.transform.IsChildOf(pahar.apaVizual)) continue;
            if (!am) { b = r.bounds; am = true; } else b.Encapsulate(r.bounds);
        }
        if (am)
        {
            float ext = Mathf.Max(b.extents.x, b.extents.z);
            if (ext > 0.005f && ext < 0.3f) raza = ext * 1.08f;
        }
        razaCalculata = true;
    }
}
