using UnityEngine;

namespace Entrenamiento
{
    /// <summary>
    /// Muestra la mano sin fisica (la que sigue exactamente a la IMU) como una sombra translucida
    /// cuando se separa de la mano fisica mas que cierto porcentaje del alcance del brazo.
    /// Histeresis entre mostrar y ocultar, y transparencia proporcional a la diferencia.
    /// Va en la prótesis objetivo (PROTESIS original).
    /// </summary>
    public class SombraProtesis : MonoBehaviour
    {
        public BrazoFisico brazo;
        [Tooltip("Material con el shader Entrenamiento/SombraProtesis.")]
        public Material materialSombra;
        public Color color = new Color(0.35f, 0.75f, 1f, 1f);

        [Header("Umbrales (% del alcance del brazo)")]
        public float mostrarPct = 8f;
        public float ocultarPct = 4f;
        [Tooltip("A partir de esta diferencia la sombra tiene su opacidad maxima.")]
        public float opacaPct = 25f;
        [Range(0.05f, 1f)] public float alfaMinimo = 0.2f;
        [Range(0.05f, 1f)] public float alfaMaximo = 0.6f;
        public float velocidadFundido = 12f;

        public bool Visible { get; private set; }
        public float Alfa => alfa;

        private Renderer[] rends;
        private MaterialPropertyBlock mpb;
        private float alfa;
        private static readonly int IdColor = Shader.PropertyToID("_Color");

        private void Awake()
        {
            mpb = new MaterialPropertyBlock();
            rends = GetComponentsInChildren<Renderer>(true);
            foreach (var r in rends)
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = materialSombra;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                r.enabled = false;
            }
        }

        private void Update()
        {
            if (brazo == null) return;
            float d = brazo.deltaPct;
            if (!Visible && d > mostrarPct) Visible = true;
            else if (Visible && d < ocultarPct) Visible = false;

            float objetivo = 0f;
            if (Visible)
                objetivo = Mathf.Lerp(alfaMinimo, alfaMaximo, Mathf.InverseLerp(mostrarPct, opacaPct, d));
            alfa = Mathf.Lerp(alfa, objetivo, 1f - Mathf.Exp(-velocidadFundido * Time.deltaTime));
            if (Mathf.Abs(alfa - objetivo) < 0.005f) alfa = objetivo;

            bool ver = alfa > 0.01f;
            var c = color; c.a = alfa;
            foreach (var r in rends)
            {
                if (r.enabled != ver) r.enabled = ver;
                if (!ver) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(IdColor, c);
                r.SetPropertyBlock(mpb);
            }
        }
    }
}
