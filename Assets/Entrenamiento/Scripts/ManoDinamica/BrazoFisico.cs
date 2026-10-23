using UnityEngine;

namespace Entrenamiento
{
    /// <summary>
    /// Brazo protesico DINAMICO (con fisica), version simple.
    /// Un unico Rigidbody compuesto (colliders de brazo, palma y dedos) que arranca en la pose del brazo
    /// objetivo (PivoteBrazo, movido por la IMU) y en cada FixedUpdate recibe la velocidad lineal y angular
    /// necesaria para alcanzar esa pose en un paso de fisica. Si la mesa u otro objeto lo bloquea, la fisica lo
    /// frena y deltaPct mide cuanto se aleja del objetivo (para mostrar la sombra).
    /// Codo, muneca y dedos se copian rigidos del objetivo (HandController).
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(Rigidbody))]
    public class BrazoFisico : MonoBehaviour
    {
        [Header("Objetivo (sin fisica, movido por la IMU)")]
        public Transform pivoteObjetivo;
        public Transform protesisObjetivo;
        public HandController manoObjetivo;
        public Transform palmaObjetivo;

        [Header("Mano fisica (clon bajo este objeto)")]
        public Transform protesisFisica;
        public HandController manoFisica;
        public Transform palmaFisica;

        [Header("Cuerpo rigido")]
        public float masaKg = 1.5f;
        [Tooltip("Tope de velocidad lineal (m/s) del hombro fisico.")]
        public float velocidadLinealMax = 3f;
        [Tooltip("Tope de velocidad angular (rad/s). Limita lo fuerte que empuja el brazo.")]
        public float velocidadAngularMax = 10f;

        [Header("Diferencia objetivo-fisica (solo lectura)")]
        public float alcanceReferenciaM = 0.371f;
        public float errorPosicionM;
        public float deltaPct;
        public float errorAnguloGrados;

        public Rigidbody Cuerpo { get; private set; }

        private float ultCodo = float.NaN, ultMuneca = float.NaN, ultCurl = float.NaN;
        private bool listo;

        private void Start()
        {
            Cuerpo = GetComponent<Rigidbody>();
            Cuerpo.mass = masaKg;
            Cuerpo.useGravity = false;
            Cuerpo.linearDamping = 0f;
            Cuerpo.angularDamping = 0f;
            Cuerpo.maxAngularVelocity = velocidadAngularMax;
            Cuerpo.maxDepenetrationVelocity = 1f;
            Cuerpo.interpolation = RigidbodyInterpolation.Interpolate;
            Cuerpo.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            // La mano fisica copia la pose local que ProtesisRig le dio al modelo objetivo
            if (protesisFisica != null && protesisObjetivo != null)
            {
                protesisFisica.localPosition = protesisObjetivo.localPosition;
                protesisFisica.localRotation = protesisObjetivo.localRotation;
                protesisFisica.localScale = protesisObjetivo.localScale;
            }
            if (manoFisica != null) manoFisica.inputSource = HandController.InputSource.Protesis;

            Reiniciar();
            listo = true;
        }

        /// <summary>Pone el cuerpo fisico exactamente en la pose del objetivo, sin velocidad.</summary>
        public void Reiniciar()
        {
            if (Cuerpo == null) Cuerpo = GetComponent<Rigidbody>();
            Cuerpo.position = pivoteObjetivo.position;
            Cuerpo.rotation = pivoteObjetivo.rotation;
            transform.SetPositionAndRotation(pivoteObjetivo.position, pivoteObjetivo.rotation);
            Cuerpo.linearVelocity = Vector3.zero;
            Cuerpo.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        private void FixedUpdate()
        {
            if (!listo || pivoteObjetivo == null) return;
            float dt = Time.fixedDeltaTime;

            // codo, muneca y dedos: copia rigida del objetivo
            if (manoObjetivo != null && manoFisica != null)
            {
                if (float.IsNaN(ultCodo) || !Mathf.Approximately(ultCodo, manoObjetivo.ElbowAngle)) { manoFisica.SetElbowAngle(manoObjetivo.ElbowAngle); ultCodo = manoObjetivo.ElbowAngle; }
                if (float.IsNaN(ultMuneca) || !Mathf.Approximately(ultMuneca, manoObjetivo.WristAngle)) { manoFisica.SetWristAngle(manoObjetivo.WristAngle); ultMuneca = manoObjetivo.WristAngle; }
                if (float.IsNaN(ultCurl) || !Mathf.Approximately(ultCurl, manoObjetivo.HandCurl)) { manoFisica.SetHandCurl(manoObjetivo.HandCurl); ultCurl = manoObjetivo.HandCurl; }
            }

            // velocidad lineal: lo necesario para llegar al hombro objetivo en este paso
            Vector3 v = (pivoteObjetivo.position - Cuerpo.position) / dt;
            if (v.magnitude > velocidadLinealMax) v = v.normalized * velocidadLinealMax;
            Cuerpo.linearVelocity = v;

            // velocidad angular: lo necesario para llegar a la orientacion objetivo en este paso
            Quaternion q = pivoteObjetivo.rotation * Quaternion.Inverse(Cuerpo.rotation);
            q.ToAngleAxis(out float angDeg, out Vector3 eje);
            if (angDeg > 180f) angDeg -= 360f;
            if (float.IsInfinity(eje.x) || float.IsNaN(eje.x)) { eje = Vector3.up; angDeg = 0f; }
            Vector3 w = eje.normalized * (angDeg * Mathf.Deg2Rad / dt);
            if (w.magnitude > velocidadAngularMax) w = w.normalized * velocidadAngularMax;
            Cuerpo.angularVelocity = w;

            // diferencia objetivo-fisica
            errorAnguloGrados = Mathf.Abs(angDeg);
            if (palmaObjetivo != null && palmaFisica != null)
            {
                errorPosicionM = Vector3.Distance(palmaObjetivo.position, palmaFisica.position);
                deltaPct = 100f * errorPosicionM / Mathf.Max(0.01f, alcanceReferenciaM);
            }
        }
    }
}
