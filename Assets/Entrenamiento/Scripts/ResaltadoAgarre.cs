using UnityEngine;

namespace Entrenamiento
{
    /// <summary>
    /// Sombreado de estado para un Grabbable: agrega una capa de resaltado
    /// (shader Entrenamiento/ResaltadoBorde) sobre el material del objeto.
    ///   Rojo  = fuera del rango de agarre de la palma.
    ///   Verde = dentro del rango (se puede agarrar).
    ///   Verde intenso = agarrado.
    /// </summary>
    [RequireComponent(typeof(Grabbable))]
    public class ResaltadoAgarre : MonoBehaviour
    {
        public Material materialResaltado;
        public Color colorFueraDeRango = new Color(1f, 0.15f, 0.15f, 0.9f);
        public Color colorEnRango = new Color(0.15f, 1f, 0.3f, 1f);
        public Color colorAgarrado = new Color(0.3f, 1f, 0.45f, 1f);
        [Range(0f, 3f)] public float intensidadFuera = 0.9f;
        [Range(0f, 3f)] public float intensidadEnRango = 1.6f;
        [Range(0f, 3f)] public float intensidadAgarrado = 2.2f;
        [Tooltip("Velocidad de transicion del color.")]
        public float velocidad = 12f;

        private static readonly int IdColor = Shader.PropertyToID("_RimColor");
        private static readonly int IdIntensidad = Shader.PropertyToID("_RimIntensity");

        private Grabbable grabbable;
        private Renderer[] renderers;
        private int[] indices;
        private MaterialPropertyBlock mpb;
        private Color colorActual, colorObjetivo;
        private float intActual, intObjetivo;

        private void Awake()
        {
            grabbable = GetComponent<Grabbable>();
            mpb = new MaterialPropertyBlock();
            if (materialResaltado == null)
            {
                var sh = Shader.Find("Entrenamiento/ResaltadoBorde");
                if (sh != null) materialResaltado = new Material(sh);
            }

            renderers = GetComponentsInChildren<Renderer>();
            indices = new int[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                var mats = renderers[i].sharedMaterials;
                var nuevos = new Material[mats.Length + 1];
                mats.CopyTo(nuevos, 0);
                nuevos[mats.Length] = materialResaltado;
                renderers[i].sharedMaterials = nuevos;
                indices[i] = mats.Length;
            }

            Objetivo(grabbable.EstadoActual);
            colorActual = colorObjetivo;
            intActual = intObjetivo;
            Aplicar();
        }

        private void OnEnable() { if (grabbable != null) grabbable.EstadoCambiado += AlCambiar; }
        private void OnDisable() { if (grabbable != null) grabbable.EstadoCambiado -= AlCambiar; }

        private void AlCambiar(Grabbable g, Grabbable.Estado e) { Objetivo(e); }

        private void Objetivo(Grabbable.Estado e)
        {
            switch (e)
            {
                case Grabbable.Estado.Agarrado: colorObjetivo = colorAgarrado; intObjetivo = intensidadAgarrado; break;
                case Grabbable.Estado.EnRango: colorObjetivo = colorEnRango; intObjetivo = intensidadEnRango; break;
                default: colorObjetivo = colorFueraDeRango; intObjetivo = intensidadFuera; break;
            }
        }

        private void Update()
        {
            if (colorActual == colorObjetivo && Mathf.Approximately(intActual, intObjetivo)) return;
            float k = 1f - Mathf.Exp(-velocidad * Time.deltaTime);
            colorActual = Color.Lerp(colorActual, colorObjetivo, k);
            intActual = Mathf.Lerp(intActual, intObjetivo, k);
            if (Mathf.Abs(intActual - intObjetivo) < 0.01f) { colorActual = colorObjetivo; intActual = intObjetivo; }
            Aplicar();
        }

        private void Aplicar()
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].GetPropertyBlock(mpb, indices[i]);
                mpb.SetColor(IdColor, colorActual);
                mpb.SetFloat(IdIntensidad, intActual);
                renderers[i].SetPropertyBlock(mpb, indices[i]);
            }
        }
    }
}
