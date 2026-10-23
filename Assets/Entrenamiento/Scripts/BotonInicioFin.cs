using TMPro;
using UnityEngine;

namespace Entrenamiento
{
    /// <summary>
    /// Boton fisico de la mesa que se pulsa con la protesis: la primera pulsacion
    /// inicia la prueba y la siguiente la termina (MedidorEntrenamiento.Alternar).
    /// Este script va en el objeto con el collider TRIGGER de pulsacion (zona
    /// apenas por encima de la tapa). Una pulsacion cuenta una sola vez hasta que
    /// la protesis sale de la zona, y hay un tiempo minimo entre pulsaciones.
    /// </summary>
    public class BotonInicioFin : MonoBehaviour
    {
        public MedidorEntrenamiento medidor;
        [Tooltip("Parte visible que se hunde al pulsar.")]
        public Transform tapa;
        public Renderer tapaRender;
        public TMP_Text etiqueta;
        [Tooltip("Cuanto se hunde la tapa (m).")]
        public float hundimiento = 0.01f;
        [Tooltip("Tiempo minimo entre pulsaciones (s).")]
        public float cooldown = 1.0f;
        public Color colorIniciar = new Color(0.20f, 0.75f, 0.30f);
        public Color colorTerminar = new Color(0.85f, 0.20f, 0.20f);

        private int contactos;
        private float tUltima = -999f;
        private Vector3 posTapa;
        private MaterialPropertyBlock mpb;
        private static readonly int idColor = Shader.PropertyToID("_BaseColor");
        private static readonly int idColorLegacy = Shader.PropertyToID("_Color");

        private void Awake()
        {
            if (tapa != null) posTapa = tapa.localPosition;
            mpb = new MaterialPropertyBlock();
        }

        private void OnEnable() { if (medidor != null) medidor.Cambio += Actualizar; }
        private void OnDisable() { if (medidor != null) medidor.Cambio -= Actualizar; }
        private void Start() { Actualizar(); }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<HandController>() == null) return;   // solo la protesis
            contactos++;
            if (contactos == 1 && Time.time - tUltima >= cooldown)
            {
                tUltima = Time.time;
                if (medidor != null) medidor.Alternar();
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.GetComponentInParent<HandController>() == null) return;
            contactos = Mathf.Max(0, contactos - 1);
        }

        private void Update()
        {
            if (tapa == null) return;
            Vector3 destino = posTapa + Vector3.down * (contactos > 0 ? hundimiento : 0f);
            tapa.localPosition = Vector3.Lerp(tapa.localPosition, destino, 1f - Mathf.Exp(-25f * Time.deltaTime));
        }

        private void Actualizar()
        {
            bool corriendo = medidor != null && medidor.EnCurso;
            Color c = corriendo ? colorTerminar : colorIniciar;
            if (tapaRender != null)
            {
                tapaRender.GetPropertyBlock(mpb);
                mpb.SetColor(idColor, c);
                mpb.SetColor(idColorLegacy, c);
                tapaRender.SetPropertyBlock(mpb);
            }
            if (etiqueta != null) etiqueta.text = corriendo ? "TERMINAR" : "INICIAR";
        }
    }
}
