using UnityEngine;

namespace Entrenamiento
{
    /// <summary>
    /// Sombreado de estados del aro (via MaterialPropertyBlock, sin duplicar materiales):
    ///   Base      = gris claro mate.
    ///   Agarrado  = verde con brillo suave.
    ///   Contacto  = rojo intenso con pulso (tiene prioridad sobre agarrado).
    /// La empuñadura (zona de agarre) mantiene su color/textura y brilla en ambar
    /// cuando la palma esta en rango para tomarla.
    /// </summary>
    [RequireComponent(typeof(AroJuego))]
    public class SombreadoAro : MonoBehaviour
    {
        [Tooltip("Renderers del cuerpo del aro (cambian de color segun el estado).")]
        public Renderer[] cuerpo;
        [Tooltip("Renderers de la empuñadura (zona de agarre).")]
        public Renderer[] empunadura;

        [Header("Estado base")]
        public Color colorBase = new Color(0.80f, 0.82f, 0.86f, 1f);
        [Header("Estado agarrado")]
        public Color colorAgarrado = new Color(0.25f, 0.85f, 0.40f, 1f);
        [ColorUsage(false, true)] public Color emisionAgarrado = new Color(0.04f, 0.30f, 0.10f, 1f);
        [Header("Estado contacto con la guia")]
        public Color colorContacto = new Color(1f, 0.15f, 0.10f, 1f);
        [ColorUsage(false, true)] public Color emisionContacto = new Color(1.6f, 0.12f, 0.05f, 1f);
        [Tooltip("Frecuencia (Hz) del pulso mientras hay contacto. 0 = sin pulso.")]
        public float pulsoContacto = 6f;
        [Header("Empuñadura")]
        [ColorUsage(false, true)] public Color emisionEmpunaduraEnRango = new Color(0.55f, 0.38f, 0.02f, 1f);
        [ColorUsage(false, true)] public Color emisionEmpunaduraAgarrada = new Color(0.20f, 0.14f, 0.01f, 1f);
        [Tooltip("Velocidad de transicion entre estados (el contacto es inmediato).")]
        public float velocidad = 14f;

        private static readonly int IdColor = Shader.PropertyToID("_BaseColor");
        private static readonly int IdEmision = Shader.PropertyToID("_EmissionColor");

        private AroJuego aro;
        private MaterialPropertyBlock mpb;
        private Color color, emision, emisionEmp;

        private void Awake()
        {
            aro = GetComponent<AroJuego>();
            mpb = new MaterialPropertyBlock();
            color = colorBase;
            emision = Color.black;
            emisionEmp = Color.black;
            Aplicar();
        }

        private void Update()
        {
            bool contacto = aro.EnContacto;
            var estado = aro.Grabbable != null ? aro.Grabbable.EstadoActual : Grabbable.Estado.FueraDeRango;

            Color cObj, eObj;
            if (contacto)
            {
                float k = pulsoContacto > 0f ? 0.7f + 0.3f * Mathf.Sin(Time.time * pulsoContacto * 2f * Mathf.PI) : 1f;
                cObj = colorContacto;
                eObj = emisionContacto * k;
            }
            else if (estado == Grabbable.Estado.Agarrado) { cObj = colorAgarrado; eObj = emisionAgarrado; }
            else { cObj = colorBase; eObj = Color.black; }

            Color empObj = estado == Grabbable.Estado.Agarrado ? emisionEmpunaduraAgarrada
                         : (estado == Grabbable.Estado.EnRango ? emisionEmpunaduraEnRango : Color.black);

            float f = 1f - Mathf.Exp(-velocidad * Time.deltaTime);
            if (contacto) { color = cObj; emision = eObj; }
            else { color = Color.Lerp(color, cObj, f); emision = Color.Lerp(emision, eObj, f); }
            emisionEmp = Color.Lerp(emisionEmp, empObj, f);
            Aplicar();
        }

        private void Aplicar()
        {
            if (cuerpo != null)
                foreach (var r in cuerpo)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(mpb);
                    mpb.SetColor(IdColor, color);
                    mpb.SetColor(IdEmision, emision);
                    r.SetPropertyBlock(mpb);
                }
            if (empunadura != null)
                foreach (var r in empunadura)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(mpb);
                    mpb.SetColor(IdEmision, emisionEmp);
                    r.SetPropertyBlock(mpb);
                }
        }
    }
}
