using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

namespace Unsteady.Tremor
{
    /// <summary>
    /// Se pune pe "Left Controller" și "Right Controller" din XR Origin.
    ///
    /// Controllerul e mișcat de casca VR în fiecare frame, deci nu putem mișca
    /// direct obiectul lui. La pornire, scriptul creează un copil "Tremor Pivot"
    /// și mută sub el modelul controllerului și interactorii (Near-Far, Poke).
    /// Apoi mișcă doar pivotul. Rezultat: mâna, raza și obiectul apucat tremură
    /// împreună, iar tracking-ul real rămâne neatins.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class TremorHand : MonoBehaviour
    {
        public enum Side { Left, Right }

        [Tooltip("Ce mână este.")]
        public Side side = Side.Right;

        [Tooltip("Cât de tare tremură mâna asta față de nivelul ales. " +
                 "Parkinson începe de obicei pe o singură parte, deci poți pune 0.5 la cealaltă mână.")]
        [Range(0f, 1.5f)] public float sideMultiplier = 1f;

        [Tooltip("Copiii al căror nume conține unul din textele astea NU tremură.")]
        public string[] exclude = { "Teleport" };

        [Header("Vibrație")]
        [Tooltip("La câte secunde trimitem un impuls nou la controller.")]
        public float hapticInterval = 0.05f;

        Transform pivot;
        HapticImpulsePlayer haptics;
        float phase;
        float noiseSeed;
        float envelope;          // 0..1, pornire / oprire lină
        float smoothedSpeed;     // viteza mâinii, netezită
        float hapticTimer;
        Vector3 lastPosition;
        TremorLevelSettings lastSettings;

        public Transform Pivot => pivot;

        void Awake()
        {
            noiseSeed = side == Side.Left ? 11.3f : 47.9f;

            pivot = new GameObject("Tremor Pivot").transform;
            pivot.SetParent(transform, false);

            var children = new List<Transform>();
            foreach (Transform child in transform)
                if (child != pivot && !IsExcluded(child.name))
                    children.Add(child);
            foreach (var child in children)
                child.SetParent(pivot, false);

            haptics = GetComponent<HapticImpulsePlayer>();
            if (haptics == null) haptics = GetComponentInChildren<HapticImpulsePlayer>(true);

            lastPosition = transform.position;
        }

        bool IsExcluded(string childName)
        {
            if (exclude == null) return false;
            foreach (var e in exclude)
                if (!string.IsNullOrEmpty(e) && childName.Contains(e))
                    return true;
            return false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Viteza mâinii (m/s), folosită ca să schimbăm tremorul când mâna se mișcă.
            float speed = (transform.position - lastPosition).magnitude / dt;
            lastPosition = transform.position;
            smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, 1f - Mathf.Exp(-dt * 5f));

            var manager = TremorManager.Instance;
            bool active = manager != null && manager.IsActive;
            if (active) lastSettings = manager.CurrentSettings;

            // Pornire / oprire treptată în ~0.5 s, ca să nu "sară" mâna.
            envelope = Mathf.MoveTowards(envelope, active ? 1f : 0f, dt * 2f);
            if (manager == null || envelope <= 0f)
            {
                pivot.localPosition = Vector3.zero;
                pivot.localRotation = Quaternion.identity;
                return;
            }

            float t = Time.time;
            bool parkinson = manager.Profile == TremorProfile.Parkinson;

            // 1) Frecvența variază puțin în timp.
            float jitter = (Mathf.PerlinNoise(t * 0.3f, noiseSeed) - 0.5f) * 2f * manager.frequencyJitter;
            float frequency = manager.BaseFrequency * (1f + jitter);
            phase += 2f * Mathf.PI * frequency * dt;
            if (phase > 1000f) phase -= 2f * Mathf.PI * 100f;

            // 2) Cum reacționează la mișcare:
            //    Parkinson = tremor de repaus: scade când miști mâna.
            //    Esențial  = tremor de acțiune: crește când miști / ții ceva.
            float move = Mathf.Clamp01(smoothedSpeed / 0.5f);
            float motionFactor = parkinson ? Mathf.Lerp(1f, 0.45f, move) : Mathf.Lerp(0.85f, 1.25f, move);

            // 3) Amplitudinea "respiră" puțin (zgomot Perlin), nu e constantă.
            float breathing = 0.75f + 0.5f * Mathf.PerlinNoise(t * 0.7f, noiseSeed + 5f);
            float k = envelope * motionFactor * sideMultiplier * breathing;

            float amp = lastSettings.amplitudeMeters * k;
            float rot = lastSettings.rotationDegrees * k;

            float s1 = Mathf.Sin(phase);
            float s2 = Mathf.Sin(phase * 1.03f + 1.3f);
            float n = (Mathf.PerlinNoise(t * 3f, noiseSeed + 9f) - 0.5f) * 2f;

            // Offset de poziție: în principal sus-jos, puțin lateral.
            pivot.localPosition = new Vector3(s2 * amp * 0.5f, s1 * amp, n * amp * 0.3f);

            // Rotație: Parkinson = răsucire a încheieturii ("numărat bani"),
            //          esențial  = flexie sus-jos a încheieturii.
            pivot.localRotation = parkinson
                ? Quaternion.Euler(s2 * rot * 0.3f, n * rot * 0.2f, s1 * rot)
                : Quaternion.Euler(s1 * rot, n * rot * 0.2f, s2 * rot * 0.3f);

            // 4) Vibrația, sincronizată cu oscilația.
            if (manager.HapticsEnabled)
            {
                hapticTimer -= dt;
                if (hapticTimer <= 0f)
                {
                    hapticTimer = hapticInterval;
                    float strength = Mathf.Clamp01(lastSettings.hapticStrength * envelope * sideMultiplier *
                                                   (0.35f + 0.65f * Mathf.Abs(s1)));
                    SendHaptic(strength, hapticInterval * 1.5f);
                }
            }
        }

        void SendHaptic(float amplitude, float duration)
        {
            if (amplitude <= 0.001f) return;
            if (haptics != null && haptics.SendHapticImpulse(amplitude, duration)) return;
            HapticsUtility.SendHapticImpulse(amplitude, duration,
                side == Side.Left ? HapticsUtility.Controller.Left : HapticsUtility.Controller.Right);
        }

        void OnDisable()
        {
            if (pivot == null) return;
            pivot.localPosition = Vector3.zero;
            pivot.localRotation = Quaternion.identity;
        }
    }
}
