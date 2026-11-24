using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Unsteady.Flux;

/// Stația 1: Toarnă apă. Implementează IStatie (Scripts/Flux/IStatie.cs).
/// Pune-l pe un obiect care RĂMÂNE ACTIV mereu (NU pe grupul cu pahare, că acela se ascunde).
///
/// Cum se joacă: torni apă din paharul plin în cel gol. Inelul de pe paharul gol urcă odată cu apa
/// și se face verde când nivelul e în zona țintă (nivelSemn ± margineEroare). Te oprești când e verde.
/// Stația se termină singură când:
///   - te-ai oprit din turnat cu apa în zona verde (după secundeStabil), sau
///   - ai lăsat paharele jos după ce ai turnat ceva (după secundeLasat), sau
///   - paharul țintă s-a umplut aproape de tot.
public class StatieTurnare : MonoBehaviour, IStatie
{
    [Header("Obiectele stației")]
    [Tooltip("Opțional: grupul cu paharele + semnul. Se arată la Pregătește și se ascunde la Oprește. Lasă gol ca paharele să rămână pe masă.")]
    public GameObject radacinaStatie;
    public Pahar paharPlin;     // pornește plin
    public Pahar paharGol;      // pornește gol, are inelul
    [Tooltip("Obiectul cu InelSemn (inelul care urcă odată cu apa).")]
    public Transform semn;

    [Header("Reguli")]
    [Tooltip("Nivelul țintă, ca fracțiune din paharul gol (0.6 = 60%).")]
    [Range(0.1f, 0.95f)] public float nivelSemn = 0.6f;
    [Tooltip("Cât de mult poți greși față de țintă și tot e bine (0.05 = ±5% din pahar, cam ±12 ml la 250 ml).")]
    [Range(0.01f, 0.15f)] public float margineEroare = 0.05f;
    [Tooltip("Câte secunde trebuie să stea apa în zona verde, fără să mai torni, ca să se termine.")]
    public float secundeStabil = 0.8f;
    [Tooltip("După câte secunde cu paharele lăsate jos se consideră terminat.")]
    public float secundeLasat = 1.5f;

    [Header("Test fără flux (doar în scena Dev_Stefan)")]
    public bool testFaraFlux = false;

    // ---- IStatie ----
    public string Nume => "Toarnă apă";
    public string Instructiuni =>
        "Ia paharul plin și toarnă apă în cel gol. Inelul urcă odată cu apa și se face <b>verde</b> când ai ajuns la nivelul potrivit. Atunci oprește-te.";
    public event Action<RezultatStatie> Gata;

    /// Apa e în zona țintă? (folosit și de InelSemn ca să se facă verde)
    public bool InZonaTinta(float nivel) => Mathf.Abs(nivel - nivelSemn) <= margineEroare;
    public bool Activa => activ;

    bool activ, aApucat, testInCurs;
    float timp, lasatDeSecunde, stabilDeSecunde;

    void Start()
    {
        if (!testFaraFlux) return;
        Gata += r => Debug.Log($"[StatieTurnare] GATA: {r.timpSecunde:F1}s, {r.greseli} {r.etichetaGreseli}, scor {r.scor:F2}, {r.detalii}");
        Pregateste(0);
        Porneste();
    }

    // încercare: 0 = fără tremor, 1 = cu tremor (tremorul îl pornește fluxul, nu noi)
    public void Pregateste(int incercare)
    {
        activ = false;
        timp = 0f;
        aApucat = false;
        lasatDeSecunde = 0f;
        stabilDeSecunde = 0f;

        if (radacinaStatie != null) radacinaStatie.SetActive(true);

        if (paharPlin != null) paharPlin.Reseteaza(1f);
        if (paharGol != null) paharGol.Reseteaza(0f);

        if (semn != null)
        {
            semn.gameObject.SetActive(true);
            var inel = semn.GetComponent<InelSemn>();
            if (inel != null)
            {
                if (inel.pahar == null) inel.pahar = paharGol;
                inel.statie = this;
            }
        }
    }

    public void Porneste()
    {
        timp = 0f;
        aApucat = false;
        lasatDeSecunde = 0f;
        stabilDeSecunde = 0f;
        activ = true;
    }

    public void Opreste()
    {
        activ = false;
        if (radacinaStatie != null) radacinaStatie.SetActive(false);
    }

    void Update()
    {
        if (!activ || paharPlin == null || paharGol == null) return;

        // F9 în Editor: toarnă singur, ca să vezi jetul și inelul fără cască
        if (Application.isEditor && Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame && !testInCurs)
            StartCoroutine(TestTurnare());

        timp += Time.deltaTime;

        bool apucat = paharPlin.EsteApucat || paharGol.EsteApucat;
        if (apucat) { aApucat = true; lasatDeSecunde = 0f; }
        else if (aApucat) lasatDeSecunde += Time.deltaTime;

        bool curge = paharPlin.Toarna || paharGol.Toarna;
        float nivel = paharGol.Nivel;

        // 1) s-a oprit din turnat cu apa în zona verde
        if (InZonaTinta(nivel) && !curge) stabilDeSecunde += Time.deltaTime;
        else stabilDeSecunde = 0f;
        if (stabilDeSecunde >= secundeStabil) { Termina(); return; }

        // 2) paharul țintă e aproape plin: nu mai are rost să continue
        if (nivel >= 0.97f && !curge) { Termina(); return; }

        // 3) a lăsat paharele jos după ce a turnat ceva
        float varsat = paharPlin.VarsatMl + paharGol.VarsatMl;
        bool aTurnat = nivel > 0.02f || varsat > 0.5f;
        if (aApucat && !apucat && lasatDeSecunde >= secundeLasat && aTurnat) Termina();
    }

    // ---------------- test în Editor (F9) ----------------

    /// <summary>Ridică paharul plin deasupra celui gol, îl înclină până se face inelul verde, apoi îl pune la loc.</summary>
    IEnumerator TestTurnare()
    {
        testInCurs = true;
        var t = paharPlin.transform;
        var rb = paharPlin.GetComponent<Rigidbody>();
        bool eraKinematic = rb != null && rb.isKinematic;
        if (rb != null) rb.isKinematic = true;

        Vector3 pozStart = t.position;
        Quaternion rotStart = t.rotation;
        Vector3 tinta = paharGol.gura != null ? paharGol.gura.position : paharGol.transform.position;
        Vector3 dir = tinta - pozStart; dir.y = 0f;
        dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.right;
        Vector3 axa = Vector3.Cross(Vector3.up, dir);   // rotind în jurul ei, gura se apleacă spre paharul gol

        Vector3 sus = tinta - dir * 0.05f + Vector3.up * 0.12f;
        for (float f = 0f; f < 1f; f += Time.deltaTime / 0.6f)
        {
            t.position = Vector3.Lerp(pozStart, sus, Mathf.SmoothStep(0f, 1f, f));
            yield return null;
        }

        float unghi = 0f;
        while (activ && !InZonaTinta(paharGol.Nivel) && unghi < 135f)
        {
            unghi += 35f * Time.deltaTime;
            t.rotation = Quaternion.AngleAxis(unghi, axa) * rotStart;
            // corectăm poziția ca jetul să cadă în mijlocul paharului gol
            if (paharPlin.PrezicereAterizare(tinta.y, out Vector3 punct))
            {
                Vector3 d = tinta - punct; d.y = 0f;
                t.position += d * Mathf.Clamp01(8f * Time.deltaTime);
            }
            yield return null;
        }

        while (unghi > 0f)
        {
            unghi = Mathf.Max(0f, unghi - 150f * Time.deltaTime);
            t.rotation = Quaternion.AngleAxis(unghi, axa) * rotStart;
            yield return null;
        }
        Vector3 pozSus = t.position;
        for (float f = 0f; f < 1f; f += Time.deltaTime / 0.6f)
        {
            t.position = Vector3.Lerp(pozSus, pozStart, Mathf.SmoothStep(0f, 1f, f));
            yield return null;
        }
        t.SetPositionAndRotation(pozStart, rotStart);
        if (rb != null) rb.isKinematic = eraKinematic;
        testInCurs = false;
    }

    void Termina()
    {
        activ = false;

        float varsat = paharPlin.VarsatMl + paharGol.VarsatMl;
        float nivel = paharGol.Nivel;

        // în zona verde = precizie 1; în afara ei scade cu cât te-ai îndepărtat
        float abatere = Mathf.Max(0f, Mathf.Abs(nivel - nivelSemn) - margineEroare);
        float precizie = 1f - Mathf.Clamp01(abatere / nivelSemn);
        float penalizare = Mathf.Clamp01(varsat / (paharPlin.capacitateMl * 0.5f));
        float scor = Mathf.Clamp01(precizie * (1f - 0.5f * penalizare));

        float tintaMl = nivelSemn * paharGol.capacitateMl;
        float margineMl = margineEroare * paharGol.capacitateMl;
        string verdict = InZonaTinta(nivel) ? "la nivelul potrivit"
                       : nivel < nivelSemn ? "prea puțin" : "prea mult";

        var rez = new RezultatStatie
        {
            timpSecunde = timp,
            greseli = Mathf.RoundToInt(varsat),
            etichetaGreseli = "ml vărsați",
            scor = scor,
            detalii = $"Ai turnat {paharGol.VolumMl:F0} ml ({verdict}); ținta era {tintaMl:F0} ± {margineMl:F0} ml.",
            sarit = false
        };

        Gata?.Invoke(rez);
    }
}
