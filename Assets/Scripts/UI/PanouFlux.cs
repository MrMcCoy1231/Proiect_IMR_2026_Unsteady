using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Unsteady.UI
{
    /// <summary>
    /// Panourile modului Empatie: tutorial, alegerea tremorului, instrucțiunile fiecărei stații,
    /// bara din timpul încercării, cardul informativ și ecranul de rezultate.
    /// Se construiește din meniul Unsteady → 7. Îl controlează EmpatieFlow.
    /// </summary>
    public class PanouFlux : MonoBehaviour
    {
        [Header("Pagini")]
        public GameObject tutorial;
        public GameObject setari;
        public GameObject instructiuni;
        public GameObject bara;
        public GameObject card;
        public GameObject rezultate;

        [Header("Tutorial")]
        public Button tutorialOk;

        [Header("Alegerea tremorului")]
        public TMP_Text setariAles;
        public Button setariContinua;

        [Header("Instrucțiuni")]
        public TMP_Text instrStatie;
        public TMP_Text instrTitlu;
        public TMP_Text instrText;
        public TMP_Text instrIncercare;
        public Image instrBadge;
        public Button instrStart;

        [Header("Bara din timpul încercării")]
        public TMP_Text baraText;
        public Button baraSari;

        [Header("Card informativ")]
        public TMP_Text cardTitlu;
        public TMP_Text cardText;
        public TMP_Text cardSursa;
        public Button cardContinua;

        [Header("Rezultate")]
        public GameObject[] rezRanduri;
        public TMP_Text[] rezNume;
        public TMP_Text[] rezFara;
        public TMP_Text[] rezCu;
        public TMP_Text rezRezumat;
        public Button rezRefa;
        public Button rezAltNivel;
        public Button rezMeniu;

        [Header("Culori (din moodboard)")]
        public Color turcoaz = new Color32(0x3A, 0xA6, 0xA0, 0xFF);
        public Color coral = new Color32(0xF2, 0x6D, 0x5B, 0xFF);

        Func<string> descriereSetari;

        void Start()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas != null && canvas.worldCamera == null) canvas.worldCamera = Camera.main;
        }

        void Update()
        {
            if (setari != null && setari.activeSelf && setariAles != null && descriereSetari != null)
                setariAles.text = descriereSetari();
        }

        void ArataDoar(GameObject pagina)
        {
            gameObject.SetActive(true);
            foreach (var p in new[] { tutorial, setari, instructiuni, bara, card, rezultate })
                if (p != null) p.SetActive(p == pagina);
        }

        static void Leaga(Button b, Action a)
        {
            if (b == null) return;
            b.onClick.RemoveAllListeners();
            if (a != null) b.onClick.AddListener(() => a());
        }

        public void Ascunde() => gameObject.SetActive(false);

        /// <summary>Pentru test în Editor (F8): apasă butonul principal al paginii afișate.</summary>
        public void ApasaButonulPrincipal()
        {
            if (!gameObject.activeInHierarchy) return;
            Button b = null;
            if (tutorial && tutorial.activeSelf) b = tutorialOk;
            else if (setari && setari.activeSelf) b = setariContinua;
            else if (instructiuni && instructiuni.activeSelf) b = instrStart;
            else if (bara && bara.activeSelf) b = baraSari;
            else if (card && card.activeSelf) b = cardContinua;
            else if (rezultate && rezultate.activeSelf) b = rezMeniu;
            if (b != null) b.onClick.Invoke();
        }

        public void ArataTutorial(Action ok)
        {
            ArataDoar(tutorial);
            Leaga(tutorialOk, ok);
        }

        public void ArataSetari(Func<string> descriere, Action continua)
        {
            descriereSetari = descriere;
            ArataDoar(setari);
            Leaga(setariContinua, continua);
        }

        public void ArataInstructiuni(int nr, int total, string titlu, string text, bool cuTremor, string nivel, Action start)
        {
            ArataDoar(instructiuni);
            if (instrStatie) instrStatie.text = $"Stația {nr} din {total}";
            if (instrTitlu) instrTitlu.text = titlu;
            if (instrText) instrText.text = text;
            if (instrIncercare)
                instrIncercare.text = cuTremor ? $"Încercarea 2 din 2 · cu tremor ({nivel})" : "Încercarea 1 din 2 · fără tremor";
            if (instrBadge) instrBadge.color = cuTremor ? coral : turcoaz;
            Leaga(instrStart, start);
        }

        public void ArataBara(string text, Action sari)
        {
            ArataDoar(bara);
            if (baraText) baraText.text = text;
            Leaga(baraSari, sari);
        }

        public void ArataCard(string titlu, string text, string sursa, Action continua)
        {
            ArataDoar(card);
            if (cardTitlu) cardTitlu.text = titlu;
            if (cardText) cardText.text = text;
            if (cardSursa) cardSursa.text = sursa;
            Leaga(cardContinua, continua);
        }

        public void ArataRezultate(IList<string> nume, IList<string> fara, IList<string> cu, string rezumat,
                                   Action refa, Action altNivel, Action meniu)
        {
            ArataDoar(rezultate);
            for (int i = 0; i < (rezRanduri?.Length ?? 0); i++)
            {
                bool are = i < nume.Count;
                if (rezRanduri[i]) rezRanduri[i].SetActive(are);
                if (!are) continue;
                if (rezNume != null && i < rezNume.Length && rezNume[i]) rezNume[i].text = nume[i];
                if (rezFara != null && i < rezFara.Length && rezFara[i]) rezFara[i].text = fara[i];
                if (rezCu != null && i < rezCu.Length && rezCu[i]) rezCu[i].text = cu[i];
            }
            if (rezRezumat) rezRezumat.text = rezumat;
            Leaga(rezRefa, refa);
            Leaga(rezAltNivel, altNivel);
            Leaga(rezMeniu, meniu);
        }
    }
}
