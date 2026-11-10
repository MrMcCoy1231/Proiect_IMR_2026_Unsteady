using UnityEngine;

public class PaharTinta : MonoBehaviour
{
    public Transform apaVizual; // Obiectul cilindru din interior
    public float vitezaUmplere = 0.05f;
    public float nivelMaximY = 0.015f; // Limita maxima stricta

    private Vector3 pozitieBaza;
    private Vector3 scalaBaza;
    private float nivelCurentY = 0f;

    void Start()
    {
        if (apaVizual != null)
        {
            pozitieBaza = apaVizual.localPosition;
            scalaBaza = apaVizual.localScale;
            nivelCurentY = 0f;

            // Seteaza apa la 0 pe Y la pornire
            apaVizual.localScale = new Vector3(scalaBaza.x, 0f, scalaBaza.z);
        }
    }

    void OnParticleCollision(GameObject other)
    {
        if (apaVizual == null) return;

        // Blocare imediata daca s-a atins sau depasit nivelul maxim
        if (nivelCurentY >= nivelMaximY) return;

        // Crestere nivel
        nivelCurentY += vitezaUmplere * Time.deltaTime;
        
        // Hard-cap strict
        if (nivelCurentY > nivelMaximY)
        {
            nivelCurentY = nivelMaximY;
        }

        // Aplicare dimensiune (X si Z ramane diametrul initial, Y urca pana la limita)
        apaVizual.localScale = new Vector3(scalaBaza.x, nivelCurentY, scalaBaza.z);

        // Compensare offset pe axa de crestere
        Vector3 offset = apaVizual.localRotation * new Vector3(0, nivelCurentY / 2f, 0);
        apaVizual.localPosition = pozitieBaza + offset;
    }
}