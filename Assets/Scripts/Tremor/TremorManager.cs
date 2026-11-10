using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
#endif

namespace Unsteady.Tremor
{
    public enum TremorLevel { Oprit = 0, Usor = 1, Moderat = 2, Sever = 3 }
    public enum TremorProfile { Parkinson = 0, Esential = 1 }

    [Serializable]
    public struct TremorLevelSettings
    {
        [Tooltip("Cât se mișcă mâna, în metri (0.01 = 1 cm).")]
        public float amplitudeMeters;
        [Tooltip("Cât se rotește mâna, în grade.")]
        public float rotationDegrees;
        [Tooltip("Puterea vibrației controllerului (0..1).")]
        [Range(0f, 1f)] public float hapticStrength;

        public TremorLevelSettings(float amp, float rot, float haptic)
        {
            amplitudeMeters = amp; rotationDegrees = rot; hapticStrength = haptic;
        }
    }

    /// <summary>
    /// "Creierul" tremorului: ține minte nivelul, profilul și dacă vibrația e pornită.
    /// Există un singur TremorManager în scenă. TremorHand (pe controllere) și
    /// TremorPanel (UI) citesc / schimbă starea de aici.
    /// </summary>
    public class TremorManager : MonoBehaviour
    {
        public static TremorManager Instance { get; private set; }

        [Header("Stare curentă")]
        [SerializeField] TremorLevel level = TremorLevel.Oprit;
        [SerializeField] TremorProfile profile = TremorProfile.Parkinson;
        [SerializeField] bool hapticsEnabled = true;

        [Header("Frecvențe (Hz)")]
        [Tooltip("Tremorul de repaus din Parkinson: ~4-6 Hz.")]
        public float parkinsonFrequency = 5f;
        [Tooltip("Tremorul esențial: ~6-12 Hz.")]
        public float essentialFrequency = 8f;
        [Tooltip("Cât variază frecvența în timp (un tremor real nu e un metronom).")]
        [Range(0f, 0.5f)] public float frequencyJitter = 0.12f;

        [Header("Intensitate pe nivel")]
        public TremorLevelSettings usor = new TremorLevelSettings(0.004f, 1.5f, 0.15f);
        public TremorLevelSettings moderat = new TremorLevelSettings(0.010f, 4f, 0.35f);
        public TremorLevelSettings sever = new TremorLevelSettings(0.022f, 8f, 0.6f);

        [Header("Taste de test în Editor (F1-F4 nivel, F5 profil, F6 vibrație)")]
        public bool keyboardShortcuts = true;

        [Header("Controller: butonul Y (stânga) schimbă nivelul")]
        public bool controllerShortcut = true;

        /// <summary>Se apelează de fiecare dată când se schimbă nivelul, profilul sau vibrația.</summary>
        public event Action Changed;

        public TremorLevel Level => level;
        public TremorProfile Profile => profile;
        public bool HapticsEnabled => hapticsEnabled;
        public bool IsActive => level != TremorLevel.Oprit;
        public float BaseFrequency => profile == TremorProfile.Parkinson ? parkinsonFrequency : essentialFrequency;

        public TremorLevelSettings CurrentSettings
        {
            get
            {
                switch (level)
                {
                    case TremorLevel.Usor: return usor;
                    case TremorLevel.Moderat: return moderat;
                    case TremorLevel.Sever: return sever;
                    default: return new TremorLevelSettings(0f, 0f, 0f);
                }
            }
        }

        public static string LevelName(TremorLevel l)
        {
            switch (l)
            {
                case TremorLevel.Usor: return "Ușor";
                case TremorLevel.Moderat: return "Moderat";
                case TremorLevel.Sever: return "Sever";
                default: return "Oprit";
            }
        }

        public static string ProfileName(TremorProfile p) =>
            p == TremorProfile.Parkinson ? "Parkinson (~5 Hz)" : "Tremor esențial (~8 Hz)";

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Tremor] Există deja un TremorManager în scenă. Îl șterg pe al doilea.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ---------- Metode publice (le apelează UI-ul) ----------

        public void SetLevel(int newLevel) => SetLevel((TremorLevel)Mathf.Clamp(newLevel, 0, 3));

        public void SetLevel(TremorLevel newLevel)
        {
            level = newLevel;
            Changed?.Invoke();
        }

        public void NextLevel() => SetLevel(((int)level + 1) % 4);

        public void SetProfile(int newProfile) => SetProfile((TremorProfile)Mathf.Clamp(newProfile, 0, 1));

        public void SetProfile(TremorProfile newProfile)
        {
            profile = newProfile;
            Changed?.Invoke();
        }

        public void ToggleProfile() =>
            SetProfile(profile == TremorProfile.Parkinson ? TremorProfile.Esential : TremorProfile.Parkinson);

        public void SetHaptics(bool on)
        {
            hapticsEnabled = on;
            Changed?.Invoke();
        }

        public void ToggleHaptics() => SetHaptics(!hapticsEnabled);

        // Dacă schimbi valorile din Inspector în Play, UI-ul se actualizează.
        void OnValidate()
        {
            if (Application.isPlaying) Changed?.Invoke();
        }

#if ENABLE_INPUT_SYSTEM
        void Update()
        {
            // Butonul Y de pe controllerul stâng: Oprit → Ușor → Moderat → Sever → Oprit.
            if (controllerShortcut)
            {
                var left = XRController.leftHand;
                var y = left != null ? left.TryGetChildControl<ButtonControl>("secondaryButton") : null;
                if (y != null && y.wasPressedThisFrame) NextLevel();
            }

            if (!keyboardShortcuts || Keyboard.current == null) return;
            var k = Keyboard.current;
            if (k.f1Key.wasPressedThisFrame) SetLevel(TremorLevel.Oprit);
            if (k.f2Key.wasPressedThisFrame) SetLevel(TremorLevel.Usor);
            if (k.f3Key.wasPressedThisFrame) SetLevel(TremorLevel.Moderat);
            if (k.f4Key.wasPressedThisFrame) SetLevel(TremorLevel.Sever);
            if (k.f5Key.wasPressedThisFrame) ToggleProfile();
            if (k.f6Key.wasPressedThisFrame) ToggleHaptics();
        }
#endif
    }
}
