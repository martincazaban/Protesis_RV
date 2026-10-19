using System;
using UnityEngine;

namespace Entrenamiento
{
    /// <summary>
    /// Logica minima del Juego del Aro (metricas para el hub).
    ///  EnInicio    : el aro esta en el platillo de inicio.
    ///  EnRecorrido : el aro despego del platillo de inicio; corre el tiempo y se cuentan toques.
    ///  Completado  : el aro se asento en el platillo final.
    /// Si el aro vuelve a asentarse en el inicio durante el recorrido, el intento se reinicia.
    /// Si el aro cae por debajo de 'alturaCaida', vuelve al platillo de inicio.
    /// </summary>
    public class JuegoAro : MonoBehaviour
    {
        public enum Fase { EnInicio, EnRecorrido, Completado }

        public AroJuego aro;
        public GuiaAro guia;
        public PlatilloAro platilloInicio;
        public PlatilloAro platilloFinal;
        [Tooltip("Altura (m, mundo) por debajo de la cual se considera que el aro se cayo.")]
        public float alturaCaida = 0.5f;
        public bool registrarEnConsola = true;

        public Fase FaseActual { get; private set; } = Fase.EnInicio;
        public float TiempoRecorrido { get; private set; }
        public int Toques { get; private set; }
        public float TiempoEnContacto { get; private set; }
        /// <summary>Avance maximo alcanzado sobre la guia (0-1).</summary>
        public float Progreso { get; private set; }
        public int Caidas { get; private set; }

        public event Action<JuegoAro> RecorridoIniciado;
        public event Action<JuegoAro> ToqueRegistrado;
        public event Action<JuegoAro> RecorridoCompletado;

        private Vector3 posInicial;
        private Quaternion rotInicial;

        private void Awake()
        {
            if (aro != null) { posInicial = aro.transform.position; rotInicial = aro.transform.rotation; }
        }

        private void OnEnable()
        {
            if (platilloInicio != null) { platilloInicio.AroDespego += AlDespegarInicio; platilloInicio.AroSeAsento += AlAsentarInicio; }
            if (platilloFinal != null) platilloFinal.AroSeAsento += AlAsentarFinal;
            if (aro != null) aro.ContactoCambiado += AlCambiarContacto;
        }

        private void OnDisable()
        {
            if (platilloInicio != null) { platilloInicio.AroDespego -= AlDespegarInicio; platilloInicio.AroSeAsento -= AlAsentarInicio; }
            if (platilloFinal != null) platilloFinal.AroSeAsento -= AlAsentarFinal;
            if (aro != null) aro.ContactoCambiado -= AlCambiarContacto;
        }

        private void AlDespegarInicio(PlatilloAro p)
        {
            if (FaseActual == Fase.Completado) return;  // se espera que vuelva a apoyarse en el inicio
            FaseActual = Fase.EnRecorrido;
            TiempoRecorrido = 0f; Toques = 0; TiempoEnContacto = 0f; Progreso = 0f;
            if (registrarEnConsola) Debug.Log("[JuegoAro] Despegue: comienza el recorrido.");
            if (RecorridoIniciado != null) RecorridoIniciado(this);
        }

        private void AlAsentarInicio(PlatilloAro p)
        {
            if (FaseActual == Fase.EnInicio) return;
            if (registrarEnConsola && FaseActual == Fase.EnRecorrido) Debug.Log("[JuegoAro] El aro volvio al inicio: intento reiniciado.");
            FaseActual = Fase.EnInicio;
        }

        private void AlAsentarFinal(PlatilloAro p)
        {
            if (FaseActual != Fase.EnRecorrido) return;
            FaseActual = Fase.Completado;
            Progreso = 1f;
            if (registrarEnConsola)
                Debug.Log(string.Format("[JuegoAro] Completado: tiempo {0:F1} s, toques {1}, tiempo en contacto {2:F2} s.", TiempoRecorrido, Toques, TiempoEnContacto));
            if (RecorridoCompletado != null) RecorridoCompletado(this);
        }

        private void AlCambiarContacto(AroJuego a, bool enContacto)
        {
            if (!enContacto || FaseActual != Fase.EnRecorrido) return;
            Toques++;
            if (ToqueRegistrado != null) ToqueRegistrado(this);
        }

        private void Update()
        {
            if (aro == null) return;
            if (FaseActual == Fase.EnRecorrido)
            {
                TiempoRecorrido += Time.deltaTime;
                if (aro.EnContacto) TiempoEnContacto += Time.deltaTime;
                if (guia != null)
                {
                    float d;
                    float p = guia.Progreso(aro.transform.position, out d);
                    if (d < 0.05f && p > Progreso) Progreso = p;
                }
            }
            if (aro.transform.position.y < alturaCaida && !aro.Grabbable.IsHeld)
            {
                Caidas++;
                if (registrarEnConsola) Debug.Log("[JuegoAro] El aro se cayo: vuelve al platillo de inicio.");
                ReiniciarAro();
            }
        }

        /// <summary>Devuelve el aro al platillo de inicio (si no esta agarrado).</summary>
        [ContextMenu("Reiniciar aro")]
        public void ReiniciarAro()
        {
            if (aro == null || aro.Grabbable.IsHeld) return;
            aro.Ubicar(posInicial, rotInicial);
            FaseActual = Fase.EnInicio;
        }
    }
}
