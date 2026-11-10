using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using Unsteady.Tremor;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XR;
#endif

namespace Unsteady.Tastatura
{
    /// <summary>
    /// Tastatura cu taste reale. Urmărește vârful degetului / controllerului
    /// (XRPokeInteractor) și apasă tasta de sub el. Sarcina: scrii cuvântul de pe
    /// ecran. Măsoară timpul și greșelile, ca să compari fără tremor vs cu tremor.
    /// </summary>
    public class TastaturaFunctionala : MonoBehaviour
    {
        [Header("Construită automat (Unsteady → 5)")]
        public Transform keySpace;
        public Vector3 up = Vector3.up, right = Vector3.right, forward = Vector3.forward;
        public Tasta[] keys;

        [Header("Ecran")]
        public TMP_Text targetText;
        public TMP_Text typedText;
        public TMP_Text statsText;

        [Header("Sarcina")]
        public string[] words = { "CASA", "APA", "MASA", "PAINE", "LAPTE", "SALUT", "CANA" };

        [Header("Apăsare (metri)")]
        [Tooltip("Cât de adânc trebuie împinsă tasta ca să se înregistreze.")]
        public float pressDepth = 0.004f;
        [Tooltip("Sub adâncimea asta tasta se consideră eliberată.")]
        public float releaseDepth = 0.0015f;
        [Tooltip("Cât coboară vizual tasta, maxim.")]
        public float maxTravel = 0.005f;

        [Header("Culori")]
        public Color keyColor = new Color(0.92f, 0.92f, 0.90f);
        public Color hoverColor = new Color(0.78f, 0.86f, 1.00f);
        public Color pressedColor = new Color(0.70f, 0.70f, 0.70f);
        public Color correctColor = new Color(0.36f, 0.84f, 0.36f);
        public Color wrongColor = new Color(0.90f, 0.30f, 0.25f);

        [Header("Rază + trigger")]
        [Tooltip("Tastele se pot apăsa și cu raza controllerului + trigger (util și în simulator).")]
        public bool rayClick = true;
        public float rayLength = 5f;

        [Header("Test în Editor")]
        [Tooltip("Tastatura PC-ului apasă tastele virtuale (doar pentru test).")]
        public bool testWithPcKeyboard = false;

        readonly List<XRPokeInteractor> pokers = new List<XRPokeInteractor>();
        readonly List<NearFarInteractor> rays = new List<NearFarInteractor>();
        readonly Dictionary<NearFarInteractor, bool> triggerWasDown = new Dictionary<NearFarInteractor, bool>();
        readonly Dictionary<XRPokeInteractor, int> held = new Dictionary<XRPokeInteractor, int>();
        float[] target;
        float refreshTimer;
        MaterialPropertyBlock mpb;

        int wordIndex, progress, errors;
        float startTime, endTime;
        bool running, done;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly int ColorId = Shader.PropertyToID("_Color");

        string Word => words.Length > 0 ? words[wordIndex % words.Length].ToUpperInvariant() : "";

        void Awake()
        {
            mpb = new MaterialPropertyBlock();
            if (keys == null) keys = new Tasta[0];
            target = new float[keys.Length];
            foreach (var k in keys)
                if (k != null) k.rend = k.GetComponent<Renderer>();
            if (keySpace == null) keySpace = transform;
            ResetWord();
        }

#if ENABLE_INPUT_SYSTEM
        void OnEnable()
        {
            if (Keyboard.current != null) Keyboard.current.onTextInput += OnPcText;
        }

        void OnDisable()
        {
            if (Keyboard.current != null) Keyboard.current.onTextInput -= OnPcText;
        }

        void OnPcText(char c)
        {
            if (!testWithPcKeyboard) return;
            c = char.ToUpperInvariant(c);
            for (int i = 0; i < keys.Length; i++)
                if (keys[i] != null && keys[i].kind == KeyKind.Caracter && keys[i].character == c)
                {
                    keys[i].click = 0.12f;
                    Press(i, InteractorHandedness.None);
                    return;
                }
        }
#endif

        void Update()
        {
            float mpu = keySpace.TransformVector(up).magnitude; // metri per unitate
            if (mpu <= 0f) return;

            refreshTimer -= Time.deltaTime;
            if (refreshTimer <= 0f)
            {
                refreshTimer = 1f;
                pokers.Clear();
                pokers.AddRange(FindObjectsByType<XRPokeInteractor>(FindObjectsSortMode.None));
                rays.Clear();
                rays.AddRange(FindObjectsByType<NearFarInteractor>(FindObjectsSortMode.None));
            }

            for (int i = 0; i < target.Length; i++)
            {
                target[i] = 0f;
                var key = keys[i];
                if (key == null) continue;
                key.hovered = false;
                if (key.click > 0f) { key.click -= Time.deltaTime; target[i] = maxTravel; }
            }

            if (rayClick) UpdateRays();

            foreach (var p in pokers)
            {
                if (p == null || !p.isActiveAndEnabled) { if (p != null) held.Remove(p); continue; }

                var tipT = p.attachTransform != null ? p.attachTransform : p.transform;
                int k = FindKey(tipT.position, mpu, out float d);
                if (k >= 0) target[k] = Mathf.Max(target[k], d);

                bool has = held.TryGetValue(p, out int h);
                if (has && (k != h || d < releaseDepth)) { held.Remove(p); has = false; }
                if (k >= 0 && !has && d >= pressDepth)
                {
                    held[p] = k;
                    Press(k, p.handedness);
                }
            }

#if ENABLE_INPUT_SYSTEM
            if (testWithPcKeyboard && Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
                for (int i = 0; i < keys.Length; i++)
                    if (keys[i] != null && keys[i].kind == KeyKind.Enter) { keys[i].click = 0.12f; Press(i, InteractorHandedness.None); break; }
#endif

            Animate(mpu);
            if (running && statsText != null)
                statsText.text = $"Greșeli: {errors}     Timp: {Time.time - startTime:0.0} s";
        }

        // Raza controllerului: tasta ochită se luminează; trigger-ul o apasă.
        void UpdateRays()
        {
            foreach (var r in rays)
            {
                if (r == null || !r.isActiveAndEnabled) continue;
                var t = r.transform;
                if (!Physics.Raycast(t.position, t.forward, out var hit, rayLength, ~0, QueryTriggerInteraction.Ignore)) continue;
                var key = hit.collider.GetComponent<Tasta>();
                if (key == null) continue;
                int i = System.Array.IndexOf(keys, key);
                if (i < 0) continue;

                key.hovered = true;
                bool down = TriggerDown(r);
                triggerWasDown.TryGetValue(r, out bool wasDown);
                triggerWasDown[r] = down;
                if (down && !wasDown)
                {
                    key.click = 0.12f;
                    target[i] = maxTravel;
                    Press(i, r.handedness);
                }
            }
        }

        // Trigger-ul controllerului de care ține raza (merge și cu XR Device Simulator).
        static bool TriggerDown(NearFarInteractor r)
        {
            bool xri = (r.uiPressInput != null && r.uiPressInput.ReadIsPerformed()) ||
                       (r.activateInput != null && r.activateInput.ReadIsPerformed());
            if (xri) return true;
#if ENABLE_INPUT_SYSTEM
            if (r.handedness == InteractorHandedness.Left) return DeviceTrigger(XRController.leftHand);
            if (r.handedness == InteractorHandedness.Right) return DeviceTrigger(XRController.rightHand);
            return DeviceTrigger(XRController.leftHand) || DeviceTrigger(XRController.rightHand);
#else
            return false;
#endif
        }

#if ENABLE_INPUT_SYSTEM
        static bool DeviceTrigger(XRController dev)
        {
            if (dev == null) return false;
            var b = dev.TryGetChildControl<ButtonControl>("triggerPressed") ?? dev.TryGetChildControl<ButtonControl>("triggerButton");
            if (b != null) return b.isPressed;
            var a = dev.TryGetChildControl<AxisControl>("trigger");
            return a != null && a.ReadValue() > 0.5f;
        }
#endif

        // Găsește tasta de sub vârf și cât de adânc e apăsată (metri).
        int FindKey(Vector3 tipWorld, float mpu, out float depthMeters)
        {
            depthMeters = 0f;
            Vector3 l = keySpace.InverseTransformPoint(tipWorld);
            for (int i = 0; i < keys.Length; i++)
            {
                var k = keys[i];
                if (k == null) continue;
                Vector3 rel = l - k.restLocalPos;
                if (Mathf.Abs(Vector3.Dot(rel, right)) > k.halfRight) continue;
                if (Mathf.Abs(Vector3.Dot(rel, forward)) > k.halfForward) continue;
                float d = (k.halfUp - Vector3.Dot(rel, up)) * mpu;
                if (d <= 0f || d > 0.03f) continue; // deasupra tastei, sau prea adânc (sub tastatură)
                depthMeters = d;
                return i;
            }
            return -1;
        }

        void Animate(float mpu)
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < keys.Length; i++)
            {
                var k = keys[i];
                if (k == null) continue;
                float goal = Mathf.Clamp(target[i], 0f, maxTravel);
                k.depth = Mathf.MoveTowards(k.depth, goal, 0.15f * dt);
                k.transform.localPosition = k.restLocalPos - up * (k.depth / mpu);

                Color c = k.depth > maxTravel * 0.4f ? pressedColor : (k.hovered ? hoverColor : keyColor);
                if (k.flash > 0f)
                {
                    k.flash -= dt;
                    c = Color.Lerp(c, k.flashColor, Mathf.Clamp01(k.flash / 0.25f));
                }
                if (k.rend != null)
                {
                    k.rend.GetPropertyBlock(mpb);
                    mpb.SetColor(BaseColorId, c);
                    mpb.SetColor(ColorId, c);
                    k.rend.SetPropertyBlock(mpb);
                }
            }
        }

        void Press(int index, InteractorHandedness hand)
        {
            var k = keys[index];
            if (hand == InteractorHandedness.Left || hand == InteractorHandedness.Right)
                HapticsUtility.SendHapticImpulse(0.3f, 0.04f,
                    hand == InteractorHandedness.Left ? HapticsUtility.Controller.Left : HapticsUtility.Controller.Right);

            switch (k.kind)
            {
                case KeyKind.Caracter:
                    HandleChar(k);
                    break;
                case KeyKind.Enter:
                    Flash(k, pressedColor);
                    if (done) NextWord();
                    break;
                default:
                    Flash(k, pressedColor);
                    break;
            }
        }

        void HandleChar(Tasta k)
        {
            string w = Word;
            if (done || w.Length == 0) { Flash(k, pressedColor); return; }
            if (!running) { running = true; startTime = Time.time; }

            if (char.ToUpperInvariant(k.character) == w[progress])
            {
                progress++;
                Flash(k, correctColor);
                if (progress >= w.Length) { done = true; running = false; endTime = Time.time; }
            }
            else
            {
                errors++;
                Flash(k, wrongColor);
            }
            RefreshDisplay();
        }

        static void Flash(Tasta k, Color c)
        {
            k.flashColor = c;
            k.flash = 0.35f;
        }

        /// <summary>Trece la următorul cuvânt.</summary>
        public void NextWord()
        {
            if (words.Length > 0) wordIndex = (wordIndex + 1) % words.Length;
            ResetWord();
        }

        /// <summary>Reîncepe cuvântul curent.</summary>
        public void ResetWord()
        {
            progress = 0; errors = 0; running = false; done = false;
            RefreshDisplay();
        }

        void RefreshDisplay()
        {
            string w = Word;
            if (targetText != null) targetText.text = $"Scrie: <b>{w}</b>";

            if (typedText != null)
            {
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < w.Length; i++)
                {
                    if (i > 0) sb.Append(' ');
                    if (i < progress) sb.Append("<color=#5CD65C>").Append(w[i]).Append("</color>");
                    else sb.Append('_');
                }
                typedText.text = sb.ToString();
            }

            if (statsText == null) return;
            if (done)
            {
                var lvl = TremorManager.Instance != null ? TremorManager.Instance.Level : TremorLevel.Oprit;
                statsText.text = $"<color=#5CD65C>Gata!</color>  Timp {endTime - startTime:0.0} s · Greșeli {errors} · Tremor: {TremorManager.LevelName(lvl)}\n" +
                                 "<size=80%>Apasă Enter pentru alt cuvânt</size>";
            }
            else if (!running)
                statsText.text = "Apasă tastele cu degetul";
        }
    }
}
