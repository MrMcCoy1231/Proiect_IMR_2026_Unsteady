using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unsteady.Tremor;

namespace Unsteady.UI
{
    /// <summary>
    /// Panoul din lume cu care alegi nivelul tremorului, profilul și vibrația.
    /// Găsește singur TremorManager-ul din scenă. Dacă nu există (ex: într-o scenă
    /// de test), butoanele merg doar vizual.
    /// </summary>
    public class TremorPanel : MonoBehaviour
    {
        [Header("Butoane nivel (Oprit, Ușor, Moderat, Sever)")]
        public Button[] levelButtons = new Button[4];

        [Header("Alte butoane")]
        public Button profileButton;
        public Button hapticsButton;

        [Header("Texte")]
        public TMP_Text statusText;
        public TMP_Text profileLabel;
        public TMP_Text hapticsLabel;

        [Header("Culori")]
        public Color normalColor = new Color(0.20f, 0.23f, 0.28f);
        public Color selectedColor = new Color(0.95f, 0.55f, 0.20f);
        public Color onColor = new Color(0.25f, 0.62f, 0.40f);

        TremorManager manager;
        int localLevel; // folosit doar când nu există manager

        void Start()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas != null && canvas.worldCamera == null) canvas.worldCamera = Camera.main;

            manager = TremorManager.Instance != null ? TremorManager.Instance : FindFirstObjectByType<TremorManager>();
            if (manager == null)
                Debug.LogWarning("[TremorPanel] Nu există TremorManager în scenă. Panoul merge doar vizual.", this);
            else
                manager.Changed += Refresh;

            for (int i = 0; i < levelButtons.Length; i++)
            {
                int index = i;
                if (levelButtons[i] != null)
                    levelButtons[i].onClick.AddListener(() => OnLevelClicked(index));
            }
            if (profileButton != null) profileButton.onClick.AddListener(() => { if (manager) manager.ToggleProfile(); });
            if (hapticsButton != null) hapticsButton.onClick.AddListener(() => { if (manager) manager.ToggleHaptics(); });

            Refresh();
        }

        void OnDestroy()
        {
            if (manager != null) manager.Changed -= Refresh;
        }

        void OnLevelClicked(int index)
        {
            if (manager != null) manager.SetLevel(index);
            else { localLevel = index; Refresh(); }
        }

        void Refresh()
        {
            int level = manager != null ? (int)manager.Level : localLevel;

            for (int i = 0; i < levelButtons.Length; i++)
            {
                if (levelButtons[i] == null) continue;
                var img = levelButtons[i].targetGraphic as Image;
                if (img != null) img.color = i == level ? selectedColor : normalColor;
            }

            if (statusText != null)
                statusText.text = "Tremor: <b>" + TremorManager.LevelName((TremorLevel)level) + "</b>";

            if (manager == null) return;

            if (profileLabel != null)
                profileLabel.text = TremorManager.ProfileName(manager.Profile);

            if (hapticsLabel != null)
                hapticsLabel.text = manager.HapticsEnabled ? "Vibrație: PORNITĂ" : "Vibrație: OPRITĂ";

            if (hapticsButton != null && hapticsButton.targetGraphic is Image hImg)
                hImg.color = manager.HapticsEnabled ? onColor : normalColor;
        }
    }
}
