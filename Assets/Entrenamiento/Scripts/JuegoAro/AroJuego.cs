using System;
using UnityEngine;

namespace Entrenamiento
{
    /// <summary>
    /// Aro del Juego del Aro. Detecta el contacto entre el cuerpo del aro y la
    /// cadena de capsulas de la guia (GuiaAro).
    ///  - La deteccion es geometrica (OverlapCapsule de cada capsula del aro contra
    ///    las capsulas de la guia), con un pequeño margen, asi tambien detecta el
    ///    contacto "apoyado" que la fisica deja a distancia ~0.
    ///  - Barrido entre el paso de fisica anterior y el actual (subpasos), para no
    ///    perder toques cuando la mano mueve el aro rapido.
    ///  - Antirrebote: un toque termina recien despues de 'tiempoRebote' sin contacto.
    /// </summary>
    [RequireComponent(typeof(Grabbable))]
    public class AroJuego : MonoBehaviour
    {
        public GuiaAro guia;
        [Tooltip("CapsuleColliders del cuerpo del aro (los que cuentan como contacto con la guia).")]
        public CapsuleCollider[] collidersAro;
        [Tooltip("Margen (m) para considerar que el aro toca la guia.")]
        public float tolerancia = 0.001f;
        [Tooltip("Tiempo (s) sin contacto para dar por terminado un toque (evita contar rebotes como toques distintos).")]
        public float tiempoRebote = 0.08f;
        [Tooltip("Subpasos maximos del barrido por paso de fisica.")]
        [Range(1, 16)] public int subpasosMax = 8;

        public bool EnContacto { get; private set; }
        public Grabbable Grabbable { get; private set; }
        public Rigidbody Rb { get; private set; }
        /// <summary>(aro, enContacto) cada vez que empieza o termina un toque.</summary>
        public event Action<AroJuego, bool> ContactoCambiado;

        private struct Segmento { public Vector3 a, b; public float r; }
        private Segmento[] segmentos;
        private float radioEnvolvente;
        private float radioMin = 0.001f;
        private readonly Collider[] buffer = new Collider[128];
        private Vector3 posPrev;
        private Quaternion rotPrev;
        private bool hayPrev;
        private float tUltimoToque = -999f;

        private void Awake()
        {
            Grabbable = GetComponent<Grabbable>();
            Rb = GetComponent<Rigidbody>();
            Rb.sleepThreshold = 0.0005f;   // que no se "duerma" colgado de la guia
            PrepararSegmentos();
        }

        private void OnEnable() { hayPrev = false; }

        private void OnDisable()
        {
            if (EnContacto)
            {
                EnContacto = false;
                if (ContactoCambiado != null) ContactoCambiado(this, false);
            }
        }

        /// <summary>Recalcula los segmentos de las capsulas del aro (espacio local del aro).</summary>
        public void PrepararSegmentos()
        {
            if (collidersAro == null) collidersAro = new CapsuleCollider[0];
            segmentos = new Segmento[collidersAro.Length];
            radioEnvolvente = 0f;
            radioMin = float.MaxValue;
            Quaternion inv = Quaternion.Inverse(transform.rotation);
            for (int i = 0; i < collidersAro.Length; i++)
            {
                var c = collidersAro[i];
                Vector3 eje = c.direction == 0 ? Vector3.right : (c.direction == 1 ? Vector3.up : Vector3.forward);
                float medio = Mathf.Max(0f, c.height * 0.5f - c.radius);
                Vector3 aW = c.transform.TransformPoint(c.center + eje * medio);
                Vector3 bW = c.transform.TransformPoint(c.center - eje * medio);
                Vector3 s = c.transform.lossyScale;
                float r = c.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                segmentos[i].a = inv * (aW - transform.position);
                segmentos[i].b = inv * (bW - transform.position);
                segmentos[i].r = r;
                radioEnvolvente = Mathf.Max(radioEnvolvente, segmentos[i].a.magnitude + r, segmentos[i].b.magnitude + r);
                radioMin = Mathf.Min(radioMin, r);
            }
            if (radioMin == float.MaxValue) radioMin = 0.001f;
        }

        private void FixedUpdate()
        {
            Vector3 pos = Rb.position;
            Quaternion rot = Rb.rotation;
            if (!hayPrev) { posPrev = pos; rotPrev = rot; hayPrev = true; }

            bool toca = Detectar(pos, rot);
            posPrev = pos;
            rotPrev = rot;

            if (toca) tUltimoToque = Time.time;
            bool nuevo = toca || (EnContacto && Time.time - tUltimoToque < tiempoRebote);
            if (nuevo != EnContacto)
            {
                EnContacto = nuevo;
                if (ContactoCambiado != null) ContactoCambiado(this, nuevo);
            }
        }

        private bool Detectar(Vector3 pos, Quaternion rot)
        {
            if (guia == null || segmentos == null || segmentos.Length == 0) return false;

            float desp = Vector3.Distance(posPrev, pos) + Quaternion.Angle(rotPrev, rot) * Mathf.Deg2Rad * radioEnvolvente;

            // fase amplia: hay alguna capsula de la guia cerca?
            Vector3 medio = (posPrev + pos) * 0.5f;
            int n = Physics.OverlapSphereNonAlloc(medio, radioEnvolvente + desp * 0.5f + tolerancia, buffer, ~0, QueryTriggerInteraction.Ignore);
            bool cerca = false;
            for (int i = 0; i < n && !cerca; i++) cerca = guia.EsParteDeLaGuia(buffer[i]);
            if (!cerca) return false;

            // fase precisa con barrido
            int pasos = Mathf.Clamp(Mathf.CeilToInt(desp / Mathf.Max(radioMin, 0.001f)), 1, subpasosMax);
            for (int k = 1; k <= pasos; k++)
            {
                float u = (float)k / pasos;
                Vector3 p = Vector3.Lerp(posPrev, pos, u);
                Quaternion q = Quaternion.Slerp(rotPrev, rot, u);
                for (int s = 0; s < segmentos.Length; s++)
                {
                    int m = Physics.OverlapCapsuleNonAlloc(p + q * segmentos[s].a, p + q * segmentos[s].b,
                        segmentos[s].r + tolerancia, buffer, ~0, QueryTriggerInteraction.Ignore);
                    for (int i = 0; i < m; i++)
                        if (guia.EsParteDeLaGuia(buffer[i])) return true;
                }
            }
            return false;
        }

        /// <summary>Teletransporta el aro (sin velocidad) a la pose indicada.</summary>
        public void Ubicar(Vector3 posicion, Quaternion rotacion)
        {
            if (!Rb.isKinematic)
            {
                Rb.linearVelocity = Vector3.zero;
                Rb.angularVelocity = Vector3.zero;
            }
            Rb.position = posicion;
            Rb.rotation = rotacion;
            transform.SetPositionAndRotation(posicion, rotacion);
            posPrev = posicion;
            rotPrev = rotacion;
            hayPrev = true;
            Rb.WakeUp();
        }
    }
}
