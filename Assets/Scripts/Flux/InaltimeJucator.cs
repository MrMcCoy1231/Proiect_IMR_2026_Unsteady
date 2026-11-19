using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
#endif

namespace Unsteady.Flux
{
    /// <summary>
    /// Înălțimea capului.
    /// Pe cască: modul Floor, adică înălțimea reală a omului (așezat sau în picioare).
    /// În simulator (fără cască): înălțimea ochilor fixă, pentru cineva așezat la masă.
    /// Se pune pe XR Origin.
    /// </summary>
    public class InaltimeJucator : MonoBehaviour
    {
        [Tooltip("Înălțimea ochilor în simulator (metri). Așezat la masă: ~1.15-1.25.")]
        public float inaltimeOchiSimulator = 1.2f;

        IEnumerator Start()
        {
            var origin = GetComponent<XROrigin>();
            if (origin == null) origin = FindFirstObjectByType<XROrigin>();
            if (origin == null) yield break;

            yield return new WaitForSeconds(0.5f); // lăsăm casca / simulatorul să se conecteze

            if (AreCascaReala())
            {
                origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
                Debug.Log("[Inaltime] Cască reală: modul Floor (înălțimea ta reală).");
            }
            else
            {
                origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Device;
                origin.CameraYOffset = inaltimeOchiSimulator;
                Debug.Log($"[Inaltime] Simulator: ochii la {inaltimeOchiSimulator:0.00} m.");
            }
        }

        static bool AreCascaReala()
        {
#if ENABLE_INPUT_SYSTEM
            foreach (var d in InputSystem.devices)
                if (d is XRHMD && !d.GetType().Name.Contains("Simulated"))
                    return true;
#endif
            return false;
        }
    }
}
