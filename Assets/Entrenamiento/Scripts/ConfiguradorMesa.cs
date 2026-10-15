using UnityEngine;

namespace Entrenamiento
{
    public enum LadoContenedor { Izquierda, Derecha }

    /// <summary>
    /// Define de que lado de la mesa va el contenedor de origen (con los objetos)
    /// y, en el lado contrario, el contenedor de destino. Solo mueve los dos
    /// contenedores en X; la placa divisoria queda siempre en el centro.
    /// </summary>
    [ExecuteAlways]
    public class ConfiguradorMesa : MonoBehaviour
    {
        [Tooltip("Lado de la mesa (visto desde el usuario) donde esta el contenedor con los objetos.")]
        public LadoContenedor ladoOrigen = LadoContenedor.Izquierda;

        public Transform contenedorOrigen;
        public Transform contenedorDestino;

        [Tooltip("Distancia en X (metros) desde el centro de la mesa hasta el centro de cada contenedor.")]
        public float separacionX = 0.38f;

        private void Awake() { Aplicar(); }

        private void OnValidate() { Aplicar(); }

        [ContextMenu("Aplicar configuracion")]
        public void Aplicar()
        {
            float signo = ladoOrigen == LadoContenedor.Izquierda ? -1f : 1f;
            Mover(contenedorOrigen, signo * separacionX);
            Mover(contenedorDestino, -signo * separacionX);
        }

        private static void Mover(Transform t, float x)
        {
            if (t == null) return;
            Vector3 p = t.localPosition;
            if (Mathf.Approximately(p.x, x)) return;
            p.x = x;
            t.localPosition = p;
        }
    }
}
