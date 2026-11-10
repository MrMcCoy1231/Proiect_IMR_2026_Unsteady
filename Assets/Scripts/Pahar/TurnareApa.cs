using UnityEngine;

public class TurnareApa : MonoBehaviour
{
    public ParticleSystem particuleApa;
    public Transform apaVizual;
    public float unghiTurnare = 25f;
    
    private Vector3 axaCorectaInSus;
    private string textDebug = "";

    void Start()
    {
        // opreste apa la inceput
        if (particuleApa != null) particuleApa.Stop();

        // scriptul invata singur care e axa corecta (ignora eroarea 3d)
        // cauta axa care e indreptata in sus (spre cer) la inceputul jocului
        Vector3[] toateAxele = { 
            transform.up, -transform.up, 
            transform.right, -transform.right, 
            transform.forward, -transform.forward 
        };

        float celMaiMicUnghi = 180f;
        foreach (Vector3 axa in toateAxele)
        {
            float unghi = Vector3.Angle(axa, Vector3.up);
            if (unghi < celMaiMicUnghi)
            {
                celMaiMicUnghi = unghi;
                // salvam axa castigatoare in coordonate locale
                axaCorectaInSus = transform.InverseTransformDirection(axa);
            }
        }
    }

    void Update()
    {
        if (particuleApa == null) return;

        // transformam axa invatata la start inapoi in orientarea curenta
        Vector3 axaCurentaInSus = transform.TransformDirection(axaCorectaInSus);
        
        // calculam unghiul real
        float unghiCurent = Vector3.Angle(axaCurentaInSus, Vector3.up);

        if (unghiCurent > unghiTurnare)
        {
           // textDebug = $"CURGE! Inclinat la {unghiCurent:F0} grade";

            if (!particuleApa.gameObject.activeSelf) particuleApa.gameObject.SetActive(true);
            if (!particuleApa.isPlaying) particuleApa.Play();

            // scade apa
            if (apaVizual != null && apaVizual.localScale.y > 0)
            {
                Vector3 s = apaVizual.localScale;
                s.y -= 0.15f * Time.deltaTime;
                if (s.y < 0) s.y = 0;
                apaVizual.localScale = s;
            }
        }
        else
        {
          //  textDebug = $"DREPT ({unghiCurent:F0} grade) - Oprit";
            if (particuleApa.isPlaying) particuleApa.Stop();
        }
    }

    // void OnGUI()
    // {
    //     GUI.color = Color.green;
    //     GUI.Label(new Rect(20, 20, 600, 40), $"<size=24>{textDebug}</size>");
    // }
}