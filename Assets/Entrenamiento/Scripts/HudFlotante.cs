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
        public MedidorEntrenamiento medidor;
        public TMP_Text texto;
        [Tooltip("Segundos entre refrescos del texto mientras corre la prueba.")]
        public float periodoRefresco = 0.1f;
        private readonly System.Text.StringBuilder sb = new System.Text.StringBuilder(256);
        private float tRefresco;

        private void OnEnable()
        {
            if (contador != null) contador.Cambio += AlContador;
            if (medidor != null) medidor.Cambio += Refrescar;
        }

        private void OnDisable()
        {
            if (contador != null) contador.Cambio -= AlContador;
            if (medidor != null) medidor.Cambio -= Refrescar;
        }

        private void Start()
        {
            Refrescar();
            Seguir(true);
        }

        private void LateUpdate()
        {
            Seguir(false);
            if (medidor != null && medidor.EnCurso && Time.time - tRefresco >= periodoRefresco) Refrescar();
        }

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

        private void AlContador(int trasladados, int total) { Refrescar(); }

        private void Refrescar()
        {
            tRefresco = Time.time;
            if (texto == null) return;
            sb.Length = 0;
            if (medidor != null)
            {
                switch (medidor.Fase)
                {
                    case FaseEntrenamiento.Espera:
                        sb.Append("<color=#9fd0ff>Pulsa el boton para iniciar</color>\n");
                        break;
                    case FaseEntrenamiento.EnCurso:
                        sb.Append("Tiempo  <b>").Append(MedidorEntrenamiento.Formato(medidor.TiempoTotal)).Append("</b>\n");
                        break;
                    default:
                        sb.Append("<color=#ffd24d>FIN</color>  Tiempo  <b>").Append(MedidorEntrenamiento.Formato(medidor.TiempoTotal)).Append("</b>\n");
                        break;
                }
            }
            int tras = contador != null ? contador.Trasladados : 0;
            int tot = contador != null ? contador.Total : 0;
            sb.Append("Trasladados  <b>").Append(tras).Append("</b> / ").Append(tot);
            if (medidor != null)
            {
                sb.Append("\nToques  <b>").Append(medidor.Toques).Append("</b>");
                sb.Append("    Soltadas  <b>").Append(medidor.Soltadas).Append("</b>");
                float enMano = medidor.TiempoPiezaEnMano();
                if (enMano >= 0f)
                    sb.Append("\nPieza  <b>").Append(enMano.ToString("0.0")).Append(" s</b>");
                else if (medidor.UltimaPieza >= 0f)
                    sb.Append("\nUltima pieza  <b>").Append(medidor.UltimaPieza.ToString("0.0")).Append(" s</b>");
                float prom, min, max;
                if (medidor.EstadisticasPiezas(out prom, out min, out max))
                    sb.Append("\nProm  ").Append(prom.ToString("0.0")).Append(" s   Mejor  ").Append(min.ToString("0.0")).Append(" s");
            }
            texto.text = sb.ToString();
        }
    }
}
