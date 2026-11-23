using System;
using UnityEngine;
using Unsteady.Flux;


/// Statia 1 — Toarna apa. Implementeaza IStatie (Scripts/Flux/IStatie.cs).
/// Pune-l pe un obiect care RAMANE ACTIV mereu (NU pe grupul cu pahare, ca acela se ascunde).

public class StatieTurnare : MonoBehaviour, IStatie
{
    [Header("Obiectele statiei")]
    [Tooltip("Grupul cu paharele + semnul. Se arata la Pregateste si se ascunde la Opreste.")]
    public GameObject radacinaStatie;
    public Pahar paharPlin;     // porneste plin
    public Pahar paharGol;      // porneste gol, are semnul de umplere
    [Tooltip("Cilindru foarte subtire (inel) care arata nivelul tinta. Il pozitioneaza scriptul.")]
    public Transform semn;

    [Header("Reguli")]
    [Range(0.1f, 0.95f)] public float nivelSemn = 0.6f;
    [Tooltip("Bifat: inelul e COPIL al paharului gol, il pui tu cu mana la inaltimea dorita, iar nivelSemn se calculeaza din pozitia lui.")]
    public bool inelLipitDePahar = true;
    [Tooltip("Dupa cate secunde cu paharele lasate jos se considera terminat")]
    public float secundeLasat = 1.5f;

    [Header("Test fara flux (doar in scena ta Dev_Stefan)")]
    public bool testFaraFlux = false;

    // ---- IStatie ----
    public string Nume => "Toarna apa";
    public string Instructiuni => "Ia paharul plin si toarna apa in cel gol, pana la semnul de pe pahar.";
    public event Action<RezultatStatie> Gata;

    bool activ, semnVizibil, aApucat;
    float timp, lasatDeSecunde;

    void Start()
    {
        if (!testFaraFlux) return;
        Gata += r => Debug.Log($"[StatieTurnare] GATA: {r.timpSecunde:F1}s, {r.greseli} {r.etichetaGreseli}, scor {r.scor:F2}, {r.detalii}");
        Pregateste(0);
        Porneste();
    }

    // incercare: 0 = fara tremor, 1 = cu tremor (tremorul il porneste fluxul, nu noi)
    public void Pregateste(int incercare)
    {
        activ = false;
        timp = 0f;
        aApucat = false;
        lasatDeSecunde = 0f;

        if (radacinaStatie != null) radacinaStatie.SetActive(true);

        paharPlin.Reseteaza(1f);
        paharGol.Reseteaza(0f);

        if (semn != null)
        {
            semn.gameObject.SetActive(true);
            var inel = semn.GetComponent<InelSemn>();
            if (inel != null)
            {
                if (inel.pahar == null) inel.pahar = paharGol;
                semnVizibil = false;
            }
            else if (inelLipitDePahar)
            {
                // inelul sta pe pahar (copil) si se misca cu el; nivelul tinta vine din pozitia lui
                nivelSemn = Mathf.Clamp(paharGol.FractiuneLaPunct(semn.position), 0.1f, 0.95f);
                semnVizibil = false;
            }
            else
            {
                semn.position = paharGol.PunctSuprafata(nivelSemn);
                semnVizibil = true;
            }
        }
    }

    public void Porneste()
    {
        timp = 0f;
        aApucat = false;
        lasatDeSecunde = 0f;
        activ = true;
    }

    public void Opreste()
    {
        activ = false;
        semnVizibil = false;
        if (semn != null) semn.gameObject.SetActive(false);
        if (radacinaStatie != null) radacinaStatie.SetActive(false);
    }

    void Update()
    {
        if (!activ) return;

        timp += Time.deltaTime;

        bool apucat = paharPlin.EsteApucat || paharGol.EsteApucat;
        if (apucat) { aApucat = true; lasatDeSecunde = 0f; }
        else if (aApucat) lasatDeSecunde += Time.deltaTime;

        // 1) a ajuns la semn
        if (paharGol.Nivel >= nivelSemn) { Termina(); return; }

        // 2) a lasat paharul jos dupa ce a turnat ceva
        float varsat = paharPlin.VarsatMl + paharGol.VarsatMl;
        bool aTurnat = paharGol.Nivel > 0.02f || varsat > 0.5f;
        if (aApucat && !apucat && lasatDeSecunde >= secundeLasat && aTurnat) Termina();
    }

    void LateUpdate()
    {
        // semnul urmareste paharul tinta (ramane orizontal)
        if (semnVizibil && semn != null && paharGol != null)
            semn.position = paharGol.PunctSuprafata(nivelSemn);
    }

    void Termina()
    {
        activ = false;

        float varsat = paharPlin.VarsatMl + paharGol.VarsatMl;
        float precizie = 1f - Mathf.Clamp01(Mathf.Abs(paharGol.Nivel - nivelSemn) / nivelSemn);
        float penalizare = Mathf.Clamp01(varsat / (paharPlin.capacitateMl * 0.5f));
        float scor = Mathf.Clamp01(precizie * (1f - 0.5f * penalizare));

        var rez = new RezultatStatie
        {
            timpSecunde = timp,
            greseli = Mathf.RoundToInt(varsat),
            etichetaGreseli = "ml varsati",
            scor = scor,
            detalii = $"Umplut {paharGol.VolumMl:F0} ml din {nivelSemn * paharGol.capacitateMl:F0} ml",
            sarit = false
        };

        Gata?.Invoke(rez);
    }
}
