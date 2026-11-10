using UnityEngine;

namespace Unsteady.Tastatura
{
    public enum KeyKind { Caracter, Spatiu, Stergere, Enter, Modificator }

    /// <summary>
    /// O tastă de pe tastatură. Se creează automat din meniul Unsteady → 5.
    /// Ține minte ce literă e și unde stă în repaus. TastaturaFunctionala o apasă.
    /// </summary>
    public class Tasta : MonoBehaviour
    {
        public string label;
        public KeyKind kind = KeyKind.Modificator;
        public char character;

        [Header("Geometrie (în unitățile modelului)")]
        public Vector3 restLocalPos;
        public float halfRight, halfUp, halfForward;

        [System.NonSerialized] public float depth;       // cât e apăsată acum (metri)
        [System.NonSerialized] public float flash;       // timp rămas din flash-ul de culoare
        [System.NonSerialized] public Color flashColor;
        [System.NonSerialized] public float click;       // apăsare scurtă (rază / test PC)
        [System.NonSerialized] public bool hovered;      // raza e pe tastă
        [System.NonSerialized] public Renderer rend;
    }
}
