using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using Unsteady.Tremor;
using Unsteady.UI;

namespace Unsteady.Flux
{
    /// <summary>
    /// Modul Empatie, cap-coadă (fluxul din propunere):
    /// Meniu → Tutorial → Alegi tremorul → pentru fiecare stație: încercarea 1 fără tremor,
    /// încercarea 2 cu tremor → card informativ → ... → Rezultate (fără vs. cu tremor).
    /// Stațiile sunt găsite automat: orice component din scenă care implementează IStatie.
    /// </summary>
    public class EmpatieFlow : MonoBehaviour
    {
        public static EmpatieFlow Instance { get; private set; }

        [Header("Legături (completate de Unsteady → 7)")]
        public PanouFlux panou;
        public StartMenu meniu;
        public GameObject panouTremor;

        [Header("Setări")]
        [Tooltip("Nivelul folosit la încercarea cu tremor, dacă utilizatorul lasă panoul pe Oprit.")]
        public TremorLevel nivelImplicit = TremorLevel.Moderat;
        [Tooltip("Pauza după o încercare, înainte de următorul panou (secunde).")]
        public float pauzaDupaIncercare = 1.2f;
        [Tooltip("Durata trecerii prin negru (secunde).")]
        public float durataFade = 0.6f;

        public bool Activ { get; private set; }

        readonly List<IStatie> statii = new List<IStatie>();
        RezultatStatie[,] rezultate;
        int statieIdx, incercare;
        TremorLevel nivelAles;
        TremorProfile profilAles;
        bool shortcutInitial;
        ScreenFade fade;
        IStatie curenta;

        TremorManager TM => TremorManager.Instance;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            if (panou != null) panou.Ascunde();
            if (Camera.main != null) fade = ScreenFade.Creeaza(Camera.main);
        }

        [Header("Test în Editor")]
        [Tooltip("F8 = pornește Empatie / apasă butonul principal al panoului (Start, Continuă, Sari peste...).")]
        public bool scurtaturaF8 = true;

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (!scurtaturaF8 || Keyboard.current == null || !Keyboard.current.f8Key.wasPressedThisFrame) return;
            if (!Activ)
            {
                if (meniu != null) meniu.StartEmpathy(); else Incepe();
            }
            else if (panou != null) panou.ApasaButonulPrincipal();
#endif
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------------- pornire / oprire ----------------

        /// <summary>Apelat de butonul "Empatie" din meniul de start.</summary>
        public void Incepe()
        {
            if (Activ) return;
            Activ = true;
            if (TM != null) { shortcutInitial = TM.controllerShortcut; TM.controllerShortcut = false; TM.SetLevel(TremorLevel.Oprit); }

            statii.Clear();
            statii.AddRange(FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).OfType<IStatie>().OrderBy(Ordine));
            foreach (var s in statii) s.Opreste();

            StartCoroutine(PrinNegru(ArataTutorial));
        }

        /// <summary>Iese din modul Empatie și se întoarce la meniu.</summary>
        public void Termina()
        {
            StartCoroutine(PrinNegru(() =>
            {
                Dezabonare();
                foreach (var s in statii) s.Opreste();
                if (panou != null) panou.Ascunde();
                if (panouTremor != null) panouTremor.SetActive(false);
                PuneInapoiPanouTremor();
                if (TM != null) { TM.SetLevel(TremorLevel.Oprit); TM.controllerShortcut = shortcutInitial; }
                Activ = false;
                if (meniu != null) meniu.ShowMenu();
            }));
        }

        // ---------------- pași ----------------

        void ArataTutorial()
        {
            if (panouTremor != null) panouTremor.SetActive(false);
            panou.ArataTutorial(ArataSetari);
        }

        Vector3 tremorPos; Quaternion tremorRot; bool tremorMutat;

        // Pe pagina "Alege tremorul", panoul de tremor stă lângă panoul fluxului (în stânga lui).
        void MutaPanouTremorLangaFlux()
        {
            if (panouTremor == null || panou == null) return;
            if (!tremorMutat) { tremorPos = panouTremor.transform.position; tremorRot = panouTremor.transform.rotation; tremorMutat = true; }
            var t = panou.transform;
            Vector3 pos = t.position - t.right * 0.8f;
            Quaternion rot = t.rotation;
            if (Camera.main != null)
            {
                // "stânga" văzută de jucător, nu stânga panoului (panoul poate fi rotit).
                Vector3 spre = t.position - Camera.main.transform.position; spre.y = 0;
                if (spre.sqrMagnitude > 0.001f)
                {
                    Vector3 dreapta = Vector3.Cross(Vector3.up, spre.normalized);
                    pos = t.position - dreapta * 0.8f;
                    Vector3 away = pos - Camera.main.transform.position; away.y = 0;
                    rot = Quaternion.LookRotation(away.normalized);   // întors spre jucător
                }
            }
            panouTremor.transform.SetPositionAndRotation(pos, rot);
        }

        void PuneInapoiPanouTremor()
        {
            if (!tremorMutat || panouTremor == null) return;
            panouTremor.transform.SetPositionAndRotation(tremorPos, tremorRot);
            tremorMutat = false;
        }

        void ArataSetari()
        {
            MutaPanouTremorLangaFlux();
            if (panouTremor != null) panouTremor.SetActive(true);
            if (TM != null && TM.Level == TremorLevel.Oprit) TM.SetLevel(nivelImplicit);
            panou.ArataSetari(DescriereTremor, () =>
            {
                nivelAles = TM != null && TM.Level != TremorLevel.Oprit ? TM.Level : nivelImplicit;
                profilAles = TM != null ? TM.Profile : TremorProfile.Parkinson;
                if (panouTremor != null) panouTremor.SetActive(false);
                PuneInapoiPanouTremor();
                if (TM != null) TM.SetLevel(TremorLevel.Oprit);
                rezultate = new RezultatStatie[Mathf.Max(1, statii.Count), 2];
                StartStatie(0);
            });
        }

        string DescriereTremor()
        {
            if (TM == null) return "";
            var lvl = TM.Level == TremorLevel.Oprit ? nivelImplicit : TM.Level;
            return $"Ales: <b>{TremorManager.LevelName(lvl)}</b> · {TremorManager.ProfileName(TM.Profile)}";
        }

        void StartStatie(int i)
        {
            statieIdx = i;
            incercare = 0;
            if (i >= statii.Count) { ArataRezultate(); return; }
            ArataInstructiuni();
        }

        void ArataInstructiuni()
        {
            curenta = statii[statieIdx];
            if (TM != null) TM.SetLevel(TremorLevel.Oprit);
            curenta.Pregateste(incercare);
            panou.ArataInstructiuni(statieIdx + 1, statii.Count, curenta.Nume, curenta.Instructiuni,
                                    incercare == 1, TremorManager.LevelName(nivelAles), PornesteIncercarea);
        }

        void PornesteIncercarea()
        {
            bool cuTremor = incercare == 1;
            if (TM != null)
            {
                TM.SetProfile(profilAles);
                TM.SetLevel(cuTremor ? nivelAles : TremorLevel.Oprit);
            }
            string stare = cuTremor ? $"<color=#F26D5B>cu tremor ({TremorManager.LevelName(nivelAles)})</color>"
                                    : "<color=#3AA6A0>fără tremor</color>";
            panou.ArataBara($"<b>{curenta.Nume}</b> · încercarea {incercare + 1} din 2 · {stare}",
                            () => LaGata(RezultatStatie.Sarit));
            curenta.Gata += LaGata;
            curenta.Porneste();
        }

        void LaGata(RezultatStatie r)
        {
            Dezabonare();
            rezultate[statieIdx, incercare] = r;
            if (TM != null) TM.SetLevel(TremorLevel.Oprit);
            StartCoroutine(DupaPauza(() =>
            {
                if (incercare == 0)
                {
                    incercare = 1;
                    ArataInstructiuni();
                }
                else
                {
                    curenta.Opreste();
                    var card = Carduri.Pentru(Ordine(curenta));
                    panou.ArataCard(card.titlu, card.text, card.sursa, () => StartStatie(statieIdx + 1));
                }
            }));
        }

        void Dezabonare()
        {
            if (curenta != null) curenta.Gata -= LaGata;
        }

        void ArataRezultate()
        {
            var nume = new List<string>();
            var fara = new List<string>();
            var cu = new List<string>();
            float tFara = 0, tCu = 0; int gFara = 0, gCu = 0, mlFara = 0, mlCu = 0; bool comparabil = true;

            for (int i = 0; i < statii.Count; i++)
            {
                nume.Add(statii[i].Nume);
                fara.Add(Format(rezultate[i, 0]));
                cu.Add(Format(rezultate[i, 1]));
                if (rezultate[i, 0].sarit || rezultate[i, 1].sarit) { comparabil = false; continue; }
                tFara += rezultate[i, 0].timpSecunde; tCu += rezultate[i, 1].timpSecunde;
                // apa vărsată (ml) nu se adună cu greșelile de tastat / ieșirile din rând
                if (EsteInMl(rezultate[i, 0])) { mlFara += rezultate[i, 0].greseli; mlCu += rezultate[i, 1].greseli; }
                else { gFara += rezultate[i, 0].greseli; gCu += rezultate[i, 1].greseli; }
            }

            string rezumat;
            if (statii.Count == 0) rezumat = "Nu există stații în scenă.";
            else if (tFara > 0.1f && tCu > 0f)
            {
                float raport = tCu / tFara;
                int extra = gCu - gFara;
                string nivel = TremorManager.LevelName(nivelAles);
                string timp = raport >= 1.05f ? $"ți-a luat de <b>{raport:0.0} ori</b> mai mult"
                            : raport <= 0.95f ? "ai fost chiar mai rapid"
                            : "ți-a luat cam la fel de mult";
                int mlExtra = mlCu - mlFara;
                string gr = extra > 0 ? $"ai făcut <b>{extra}</b> {(extra == 1 ? "greșeală" : "greșeli")} în plus"
                          : extra < 0 ? "ai făcut mai puține greșeli" : "ai făcut tot atâtea greșeli";
                string apa = mlExtra > 0 ? $" și ai vărsat <b>{mlExtra} ml</b> de apă în plus" : "";
                rezumat = $"Cu tremor <b>{nivel}</b> {timp}, {gr}{apa}.";
                if (!comparabil) rezumat += " <size=80%>(fără stațiile sărite)</size>";
            }
            else rezumat = "Ai sărit peste stații, așa că nu putem compara.";

            panou.ArataRezultate(nume, fara, cu, rezumat,
                refa: () => { rezultate = new RezultatStatie[Mathf.Max(1, statii.Count), 2]; StartStatie(0); },
                altNivel: ArataSetari,
                meniu: Termina);
        }

        static bool EsteInMl(RezultatStatie r) =>
            !string.IsNullOrEmpty(r.etichetaGreseli) && r.etichetaGreseli.StartsWith("ml");

        static string Format(RezultatStatie r)
        {
            if (r.sarit) return "<color=#888888>sărit</color>";
            string et = string.IsNullOrEmpty(r.etichetaGreseli) ? "greșeli" : r.etichetaGreseli;
            return $"{r.timpSecunde:0.0} s · {r.greseli} {et}";
        }

        // ---------------- utilitare ----------------

        /// <summary>Ordinea stațiilor după nume: turnare → tastatură → tablă → altele.</summary>
        public static int Ordine(IStatie s)
        {
            string n = (s.Nume ?? "").ToLowerInvariant();
            if (n.Contains("toarn") || n.Contains("apa") || n.Contains("apă") || n.Contains("pahar")) return 0;
            if (n.Contains("tast")) return 1;
            if (n.Contains("tabl") || n.Contains("scri")) return 2;
            return 3;
        }

        IEnumerator PrinNegru(Action laMijloc)
        {
            if (fade != null) yield return fade.La(1f, durataFade * 0.5f);
            laMijloc?.Invoke();
            if (fade != null) yield return fade.La(0f, durataFade * 0.5f);
        }

        IEnumerator DupaPauza(Action a)
        {
            yield return new WaitForSeconds(pauzaDupaIncercare);
            a?.Invoke();
        }

        // ---------------- cardurile informative ----------------

        /// <summary>
        /// "Știai că?" — câte un fapt (cu sursă) după fiecare stație, ales la întâmplare din lista ei.
        /// Adăugați liber fapte noi aici; țineți-le scurte (max ~2 rânduri) și cu sursă.
        /// </summary>
        static class Carduri
        {
            public struct Card { public string titlu, text, sursa; }

            static Card C(string text, string sursa) => new Card { titlu = "Știai că?", text = text, sursa = "Sursa: " + sursa };

            static readonly Card[][] Liste =
            {
                // 0 — după turnarea apei
                new[]
                {
                    C("Mulți oameni cu tremor folosesc căni cu capac și pai sau umplu paharul doar pe jumătate, ca să nu verse.",
                      "International Essential Tremor Foundation"),
                    C("Tremorul din Parkinson apare mai ales când mâna stă <b>în repaus</b> și adesea scade când o miști. De aceea, uneori e mai greu să ții paharul nemișcat decât să-l duci la gură.",
                      "Parkinson's Foundation"),
                    C("Există linguri care se stabilizează singure: au senzori și motoare care mișcă lingura în sens invers tremorului (de ex. Liftware).",
                      "Verily — Liftware"),
                    C("Cafeaua, oboseala și stresul pot face tremorul esențial mai puternic pentru o vreme.",
                      "NINDS (National Institute of Neurological Disorders and Stroke)"),
                },
                // 1 — după tastatură
                new[]
                {
                    C("Windows are opțiunea <b>Filter Keys</b>, care ignoră apăsările scurte sau repetate din greșeală. E făcută exact pentru mâini care tremură.",
                      "Microsoft Support — Accessibility"),
                    C("iPhone-ul are <b>Touch Accommodations</b>: poți seta cât timp trebuie ținut degetul pe ecran și să ignore atingerile repetate.",
                      "Apple Support — Accessibility"),
                    C("Actorul Michael J. Fox a fost diagnosticat cu boala Parkinson la doar <b>29 de ani</b>. Fundația lui finanțează azi cercetare din toată lumea.",
                      "The Michael J. Fox Foundation"),
                    C("Tremorul esențial e una dintre cele mai frecvente tulburări de mișcare: afectează cam <b>1 din 100</b> de oameni, și mult mai mulți după 65 de ani.",
                      "Louis & Ferreira, Movement Disorders (2010)"),
                },
                // 2 — după scrisul pe tablă
                new[]
                {
                    C("În boala Parkinson scrisul devine adesea tot mai mic și mai înghesuit (<b>micrografie</b>). Pixurile groase și mai grele pot ajuta.",
                      "Parkinson's Foundation"),
                    C("Medicii le cer pacienților să deseneze o <b>spirală</b> pe hârtie: forma liniei arată cât de puternic e tremorul și de ce tip e.",
                      "Haubenberger et al., Movement Disorders (2011)"),
                    C("Boala poartă numele medicului englez <b>James Parkinson</b>, care a descris-o în 1817 în „An Essay on the Shaking Palsy”.",
                      "Parkinson, J. (1817) — An Essay on the Shaking Palsy"),
                    C("<b>11 aprilie</b> e Ziua Mondială Parkinson, ziua de naștere a lui James Parkinson.",
                      "Parkinson's Europe"),
                },
                // 3 — oricare altă stație
                new[]
                {
                    C("Cam <b>3 din 4</b> persoane cu boala Parkinson au tremor la un moment dat. Restul au alte simptome, ca rigiditatea sau mișcările lente.",
                      "Hughes et al., Neurology (1993)"),
                    C("Tremorul tipic din Parkinson are cam <b>4–6 oscilații pe secundă</b>. Tremorul esențial e de obicei mai rapid.",
                      "Bhatia et al., MDS Consensus on Tremor, Movement Disorders (2018)"),
                },
            };

            static readonly int[] ultimul = { -1, -1, -1, -1 };

            public static Card Pentru(int ordine)
            {
                int k = Mathf.Clamp(ordine, 0, Liste.Length - 1);
                var lista = Liste[k];
                int i = UnityEngine.Random.Range(0, lista.Length);
                if (lista.Length > 1 && i == ultimul[k]) i = (i + 1) % lista.Length; // nu același fapt de două ori la rând
                ultimul[k] = i;
                return lista[i];
            }
        }
    }
}
