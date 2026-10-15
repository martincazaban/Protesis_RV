using TMPro;
using UnityEngine;

namespace Entrenamiento
{
    /// <summary>
    /// HUD pequeno que flota en la esquina superior izquierda del campo de
    /// vision. Sigue a la cabeza con un suavizado leve (no queda "pegado"
    /// rigidamente a la vista, lo que reduce el mareo en VR).
    /// Por ahora muestra solo los objetos trasladados.
    /// </summary>
    public class HudFlotante : MonoBehaviour
    {
        [Tooltip("Cabeza / camara del usuario. Vacio = Camera.main.")]
        public Transform cabeza;
        [Tooltip("Posicion relativa a la cabeza (x izquierda negativa, y arriba, z adelante), en metros.")]
        public Vector3 offset = new Vector3(-0.17f, 0.12f, 0.6f);
        [Tooltip("Suavizado del seguimiento (mayor = mas rapido).")]
        public float suavizado = 10f;

        public ContadorTraslados contador;
        public TMP_Text texto;

        private void OnEnable()
        {
            if (contador != null) contador.Cambio += Mostrar;
        }

        private void OnDisable()
        {
            if (contador != null) contador.Cambio -= Mostrar;
        }

        private void Start()
        {
            if (contador != null) Mostrar(contador.Trasladados, contador.Total);
            Seguir(true);
        }

        private void LateUpdate() { Seguir(false); }

        private void Seguir(bool inmediato)
        {
            if (cabeza == null && Camera.main != null) cabeza = Camera.main.transform;
            if (cabeza == null) return;
            Vector3 pos = cabeza.TransformPoint(offset);
            Quaternion rot = Quaternion.LookRotation(pos - cabeza.position, cabeza.up);
            if (inmediato) { transform.SetPositionAndRotation(pos, rot); return; }
            float k = 1f - Mathf.Exp(-suavizado * Time.deltaTime);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, pos, k),
                                             Quaternion.Slerp(transform.rotation, rot, k));
        }

        private void Mostrar(int trasladados, int total)
        {
            if (texto != null) texto.text = "Trasladados  <b>" + trasladados + "</b> / " + total;
        }
    }
}
