using System;
using UnityEngine;

namespace Entrenamiento
{
    /// <summary>
    /// Platillo (base) del Juego del Aro, fijo a la mesa en un extremo de la guia.
    /// El origen del objeto esta en el centro de la cara superior.
    ///  - "Encima": el centro del aro esta dentro del cilindro de deteccion sobre el platillo.
    ///  - "Asentado": encima y casi quieto durante 'tiempoAsentamiento' segundos.
    ///  - "Despego": estaba encima y salio de la zona (inicio del recorrido).
    /// </summary>
    public class PlatilloAro : MonoBehaviour
    {
        public enum Tipo { Inicio, Final }

        public Tipo tipo = Tipo.Inicio;
        public AroJuego aro;
        [Tooltip("Radio horizontal (m) de la zona de deteccion sobre el platillo.")]
        public float radioDeteccion = 0.035f;
        [Tooltip("Altura maxima (m) del centro del aro sobre la cara superior para considerarlo encima.")]
        public float alturaDeteccion = 0.02f;
        [Tooltip("Velocidad maxima (m/s) del aro para considerarlo quieto.")]
        public float velocidadAsentado = 0.04f;
        [Tooltip("Tiempo (s) que el aro debe permanecer quieto encima para considerarlo asentado.")]
        public float tiempoAsentamiento = 0.4f;

        public bool AroEncima { get; private set; }
        public bool AroAsentado { get; private set; }

        public event Action<PlatilloAro> AroSeAsento;
        public event Action<PlatilloAro> AroDespego;

        private float tQuieto;

        private void Start() { Evaluar(true); }

        private void FixedUpdate() { Evaluar(false); }

        private void Evaluar(bool inicial)
        {
            if (aro == null || aro.Rb == null) return;
            Vector3 local = transform.InverseTransformPoint(aro.Rb.position);
            bool encima = local.y >= -0.01f && local.y <= alturaDeteccion &&
                          new Vector2(local.x, local.z).magnitude <= radioDeteccion;

            if (encima)
            {
                bool quieto = aro.Rb.linearVelocity.magnitude <= velocidadAsentado;
                tQuieto = quieto ? tQuieto + Time.fixedDeltaTime : 0f;
                if (inicial) tQuieto = tiempoAsentamiento;
                AroEncima = true;
                if (!AroAsentado && tQuieto >= tiempoAsentamiento)
                {
                    AroAsentado = true;
                    if (!inicial && AroSeAsento != null) AroSeAsento(this);
                }
            }
            else
            {
                bool estaba = AroEncima;
                AroEncima = false;
                AroAsentado = false;
                tQuieto = 0f;
                if (estaba && AroDespego != null) AroDespego(this);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = tipo == Tipo.Inicio ? new Color(0.2f, 1f, 0.3f, 0.5f) : new Color(0.2f, 0.5f, 1f, 0.5f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0f, alturaDeteccion * 0.5f, 0f), new Vector3(radioDeteccion * 2f, alturaDeteccion, radioDeteccion * 2f));
        }
    }
}
