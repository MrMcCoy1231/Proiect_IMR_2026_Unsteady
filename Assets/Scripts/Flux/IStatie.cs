using System;

namespace Unsteady.Flux
{
    /// <summary>
    /// Contractul pe care îl respectă fiecare stație din modul Empatie
    /// (turnare apă, tastatură, scris pe tablă). EmpatieFlow le pornește pe rând,
    /// de două ori fiecare: încercarea 0 fără tremor, încercarea 1 cu tremor.
    /// Tremorul îl pornește/oprește fluxul, stația nu trebuie să știe de el.
    /// </summary>
    public interface IStatie
    {
        /// <summary>Numele afișat, ex: "Toarnă apa", "Tastează", "Scrie pe tablă".</summary>
        string Nume { get; }

        /// <summary>Ce are de făcut utilizatorul, într-o frază sau două.</summary>
        string Instructiuni { get; }

        /// <summary>Resetează stația și o face vizibilă (pahar plin, tablă ștearsă...).
        /// incercare: 0 = fără tremor, 1 = cu tremor. Folosiți aceeași sarcină la ambele, ca să se compare corect.</summary>
        void Pregateste(int incercare);

        /// <summary>Începe încercarea (pornește cronometrul).</summary>
        void Porneste();

        /// <summary>Stația s-a terminat definitiv: ascunde textele / UI-ul ei.</summary>
        void Opreste();

        /// <summary>Trimis o singură dată, când utilizatorul a terminat încercarea.</summary>
        event Action<RezultatStatie> Gata;
    }

    [Serializable]
    public struct RezultatStatie
    {
        public float timpSecunde;
        public int greseli;          // taste greșite, apă vărsată, ieșiri din linie...
        public string etichetaGreseli; // ex: "greșeli", "ml vărsați"
        public float scor;           // 0..1, cât de bine a mers
        public string detalii;       // text scurt pentru ecranul de rezultate
        public bool sarit;           // utilizatorul a apăsat "Sari peste"

        public static RezultatStatie Sarit => new RezultatStatie { sarit = true };
    }
}
