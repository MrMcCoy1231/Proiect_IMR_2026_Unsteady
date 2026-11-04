using UnityEngine;
using UnityEngine.UI;
using Unsteady.Tremor;

namespace Unsteady.UI
{
    /// <summary>
    /// Meniul de start: alegi între "Empatie" și "Testează-ți mâna".
    /// Empatie: ascunde meniul și arată obiectele din experiență (ex: PanouTremor).
    /// Testează-ți mâna: deocamdată arată un mesaj "în curând".
    /// </summary>
    public class StartMenu : MonoBehaviour
    {
        [Header("Pagini")]
        public GameObject mainPage;
        public GameObject comingSoonPage;

        [Header("Butoane")]
        public Button empathyButton;
        public Button testButton;
        public Button backButton;

        [Header("Experiența de empatie")]
        [Tooltip("Obiecte care apar doar după ce alegi Empatie (ex: PanouTremor).")]
        public GameObject[] showOnEmpathy;
        [Tooltip("Nivelul de tremor cu care pornește experiența.")]
        public TremorLevel empathyStartLevel = TremorLevel.Oprit;

        void Awake()
        {
            if (empathyButton != null) empathyButton.onClick.AddListener(StartEmpathy);
            if (testButton != null) testButton.onClick.AddListener(ShowComingSoon);
            if (backButton != null) backButton.onClick.AddListener(ShowMenu);
            ShowMenu();
        }

        void Start()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas != null && canvas.worldCamera == null) canvas.worldCamera = Camera.main;
        }

        /// <summary>Arată meniul principal, ascunde experiența și oprește tremorul.</summary>
        public void ShowMenu()
        {
            SetActive(showOnEmpathy, false);
            if (TremorManager.Instance != null) TremorManager.Instance.SetLevel(TremorLevel.Oprit);
            gameObject.SetActive(true);
            if (mainPage != null) mainPage.SetActive(true);
            if (comingSoonPage != null) comingSoonPage.SetActive(false);
        }

        public void StartEmpathy()
        {
            SetActive(showOnEmpathy, true);
            if (TremorManager.Instance != null) TremorManager.Instance.SetLevel(empathyStartLevel);
            gameObject.SetActive(false);
        }

        public void ShowComingSoon()
        {
            if (mainPage != null) mainPage.SetActive(false);
            if (comingSoonPage != null) comingSoonPage.SetActive(true);
        }

        static void SetActive(GameObject[] objects, bool on)
        {
            if (objects == null) return;
            foreach (var o in objects)
                if (o != null) o.SetActive(on);
        }
    }
}
