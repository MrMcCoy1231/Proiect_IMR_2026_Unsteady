using UnityEngine;

/// <summary>
/// Deseneaza un inel subtire (cerc orizontal) in jurul originii obiectului
/// si il deplaseaza pe inaltime odata cu nivelul apei din pahar.
/// Pune-l pe obiectul SemnNivel, impreuna cu un LineRenderer.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class InelSemn : MonoBehaviour
{
    [Header("Referinte")]
    [Tooltip("Paharul a carui apa o urmareste inelul. Daca e lasat gol, se cauta automat.")]
    public Pahar pahar;

    [Header("Dimensiuni")]
    [Tooltip("Raza inelului in metri: putin mai mare decat raza exterioara a paharului")]
    public float raza = 0.045f;
    public float grosime = 0.003f;
    public int segmente = 40;

    [Header("Comportament")]
    [Tooltip("Daca e bifat, inelul se ascunde cand paharul nu are apa")]
    public bool ascundeCandEGol = false;

    LineRenderer lr;

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.widthMultiplier = grosime;
        lr.positionCount = segmente;
        for (int i = 0; i < segmente; i++)
        {
            float a = i / (float)segmente * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * raza, 0f, Mathf.Sin(a) * raza));
        }

        GasestePahar();
    }

    void GasestePahar()
    {
        if (pahar != null) return;

        pahar = GetComponentInParent<Pahar>();
        if (pahar != null) return;

#if UNITY_2023_1_OR_NEWER
        var statie = FindAnyObjectByType<StatieTurnare>();
#else
        var statie = FindObjectOfType<StatieTurnare>();
#endif
        if (statie != null && statie.paharGol != null)
        {
            pahar = statie.paharGol;
        }
    }

    void LateUpdate()
    {
        if (pahar == null)
        {
            GasestePahar();
            if (pahar == null) return;
        }

        bool areApa = pahar.Nivel > 0.003f;
        if (ascundeCandEGol && lr != null)
        {
            if (lr.enabled != areApa) lr.enabled = areApa;
            if (!areApa) return;
        }

        transform.position = pahar.PunctSuprafata(pahar.Nivel);

        if (pahar.apaVizual != null)
            transform.rotation = pahar.apaVizual.rotation;
        else
            transform.rotation = pahar.transform.rotation;
    }
}