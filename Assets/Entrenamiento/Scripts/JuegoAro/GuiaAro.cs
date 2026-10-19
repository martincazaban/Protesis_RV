using System.Collections.Generic;
using UnityEngine;

namespace Entrenamiento
{
    /// <summary>
    /// Guia (barra metalica) del Juego del Aro.
    /// Genera por codigo:
    ///  - la malla tubular de la barra, que sigue una curva suave en S
    ///    (poste vertical -> curva -> tramo central ondulado en Y y en Z -> curva -> poste),
    ///  - una cadena continua de CapsuleColliders (se solapan en las uniones),
    ///  - y ubica los platillos de inicio y final en los extremos.
    /// Espacio local: X = de izquierda a derecha, Y = altura sobre la cara superior
    /// de los platillos, Z = profundidad. El origen es el punto medio entre los platillos.
    /// Para cambiar la forma: modificar los parametros y usar el menu contextual
    /// del componente "Regenerar guia".
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class GuiaAro : MonoBehaviour
    {
        [Header("Forma")]
        [Tooltip("Distancia (m) entre los centros de los dos platillos.")]
        public float separacionExtremos = 0.46f;
        [Tooltip("Tramo vertical recto (m) que sale de cada platillo.")]
        public float alturaPoste = 0.045f;
        [Tooltip("Radio (m) de la curva que une cada poste con el tramo central.")]
        public float radioCurvaPoste = 0.05f;
        [Tooltip("Cantidad de jorobas (subidas y bajadas) en Y del tramo central.")]
        [Min(1)] public int ondasY = 2;
        [Tooltip("Elevacion (m) de las jorobas respecto de la base del tramo central.")]
        public float amplitudY = 0.04f;
        [Tooltip("Barrido lateral maximo (m) de la S vista desde arriba (eje Z local).")]
        public float amplitudZ = 0.045f;

        [Header("Barra")]
        [Tooltip("Diametro de la barra (m).")]
        public float diametro = 0.01f;
        [Min(6)] public int ladosTubo = 16;
        [Tooltip("Separacion (m) entre anillos de la malla.")]
        public float pasoMalla = 0.004f;
        [Tooltip("Largo (m) de cada capsula de la cadena de colliders.")]
        public float largoCapsula = 0.015f;
        public PhysicsMaterial materialFisico;

        [Header("Platillos")]
        public Transform platilloInicio;
        public Transform platilloFinal;
        [Tooltip("Cuanto se hunde la barra dentro de cada platillo (m).")]
        public float hundimiento = 0.004f;

#if UNITY_EDITOR
        [Header("Editor")]
        public string rutaMalla = "Assets/Entrenamiento/Models/JuegoAro/Guia_Aro.asset";
#endif

        [SerializeField, HideInInspector] private Vector3[] muestras;
        [SerializeField, HideInInspector] private float largoTotal;
        [SerializeField, HideInInspector] private Transform contenedorCapsulas;
        [SerializeField, HideInInspector] private List<CapsuleCollider> capsulas = new List<CapsuleCollider>();

        private HashSet<Collider> conjunto;

        public float Radio { get { return diametro * 0.5f; } }
        public float LargoTotal { get { return largoTotal; } }
        public IList<CapsuleCollider> Capsulas { get { return capsulas; } }

        private void Awake() { ConstruirConjunto(); }

        private void ConstruirConjunto()
        {
            conjunto = new HashSet<Collider>();
            foreach (var c in capsulas) if (c != null) conjunto.Add(c);
        }

        /// <summary>True si el collider pertenece a la cadena de capsulas de la guia.</summary>
        public bool EsParteDeLaGuia(Collider c)
        {
            if (conjunto == null) ConstruirConjunto();
            return c != null && conjunto.Contains(c);
        }

        /// <summary>Progreso (0 = inicio, 1 = final) del punto de la guia mas cercano.</summary>
        public float Progreso(Vector3 puntoMundo, out float distancia)
        {
            distancia = float.MaxValue;
            if (muestras == null || muestras.Length < 2) return 0f;
            Vector3 p = transform.InverseTransformPoint(puntoMundo);
            int mejor = 0;
            float dMin = float.MaxValue;
            for (int i = 0; i < muestras.Length; i++)
            {
                float d = (muestras[i] - p).sqrMagnitude;
                if (d < dMin) { dMin = d; mejor = i; }
            }
            distancia = Mathf.Sqrt(dMin) * transform.lossyScale.x;
            return (float)mejor / (muestras.Length - 1);
        }

        // ------------------------------------------------------------------
        //  Curva
        // ------------------------------------------------------------------
        /// <summary>Polilinea densa (espacio local) de la linea central de la barra.</summary>
        public List<Vector3> CalcularCurva()
        {
            var p = new List<Vector3>();
            float h = separacionExtremos * 0.5f;
            float x0 = -h, x1 = h;
            float rb = Mathf.Min(radioCurvaPoste, h * 0.45f);
            float yb = alturaPoste + rb;          // altura base del tramo central
            const float paso = 0.002f;
            const float normS = 0.6495191f;       // max de sin(2pi t) * sin^2(pi t)

            // poste izquierdo (sube)
            int n = Mathf.Max(2, Mathf.CeilToInt((alturaPoste + hundimiento) / paso));
            for (int i = 0; i < n; i++)
                p.Add(new Vector3(x0, Mathf.Lerp(-hundimiento, alturaPoste, (float)i / n), 0f));
            // curva izquierda (vertical -> horizontal)
            n = Mathf.Max(8, Mathf.CeilToInt(rb * Mathf.PI * 0.5f / paso));
            for (int i = 0; i < n; i++)
            {
                float a = (float)i / n * Mathf.PI * 0.5f;
                p.Add(new Vector3(x0 + rb * (1f - Mathf.Cos(a)), alturaPoste + rb * Mathf.Sin(a), 0f));
            }
            // tramo central: jorobas en Y + S en Z (pendiente nula en los extremos)
            float xa = x0 + rb, xb = x1 - rb;
            n = Mathf.Max(16, Mathf.CeilToInt((xb - xa) / paso));
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float y = yb + amplitudY * (1f - Mathf.Cos(2f * Mathf.PI * ondasY * t)) * 0.5f;
                float s = Mathf.Sin(Mathf.PI * t);
                float z = -amplitudZ * Mathf.Sin(2f * Mathf.PI * t) * s * s / normS;
                p.Add(new Vector3(Mathf.Lerp(xa, xb, t), y, z));
            }
            // curva derecha (horizontal -> vertical)
            n = Mathf.Max(8, Mathf.CeilToInt(rb * Mathf.PI * 0.5f / paso));
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.PI * 0.5f - (float)i / n * Mathf.PI * 0.5f;
                p.Add(new Vector3(x1 - rb * (1f - Mathf.Cos(a)), alturaPoste + rb * Mathf.Sin(a), 0f));
            }
            // poste derecho (baja)
            n = Mathf.Max(2, Mathf.CeilToInt((alturaPoste + hundimiento) / paso));
            for (int i = 0; i <= n; i++)
                p.Add(new Vector3(x1, Mathf.Lerp(alturaPoste, -hundimiento, (float)i / n), 0f));
            return p;
        }

        private static Vector3[] Remuestrear(List<Vector3> poli, float paso, out float largo)
        {
            var acum = new float[poli.Count];
            for (int i = 1; i < poli.Count; i++) acum[i] = acum[i - 1] + Vector3.Distance(poli[i - 1], poli[i]);
            largo = acum[acum.Length - 1];
            int n = Mathf.Max(2, Mathf.CeilToInt(largo / paso) + 1);
            var r = new Vector3[n];
            int j = 1;
            for (int i = 0; i < n; i++)
            {
                float s = largo * i / (n - 1);
                while (j < acum.Length - 1 && acum[j] < s) j++;
                float tramo = acum[j] - acum[j - 1];
                float u = tramo > 1e-7f ? (s - acum[j - 1]) / tramo : 0f;
                r[i] = Vector3.Lerp(poli[j - 1], poli[j], Mathf.Clamp01(u));
            }
            return r;
        }

        // ------------------------------------------------------------------
        //  Generacion
        // ------------------------------------------------------------------
        [ContextMenu("Regenerar guia")]
        public void Regenerar()
        {
            var poli = CalcularCurva();
            muestras = Remuestrear(poli, pasoMalla, out largoTotal);

            Mesh malla = ConstruirTubo(muestras, Radio, ladosTubo);
            malla.name = "Guia_Aro";
#if UNITY_EDITOR
            if (!Application.isPlaying && !string.IsNullOrEmpty(rutaMalla))
                malla = GuardarMalla(malla, rutaMalla);
#endif
            GetComponent<MeshFilter>().sharedMesh = malla;

            ConstruirCapsulas();
            UbicarPlatillos();
            ConstruirConjunto();
        }

        private void UbicarPlatillos()
        {
            float h = separacionExtremos * 0.5f;
            if (platilloInicio != null)
            {
                platilloInicio.position = transform.TransformPoint(new Vector3(-h, 0f, 0f));
                platilloInicio.rotation = transform.rotation;
            }
            if (platilloFinal != null)
            {
                platilloFinal.position = transform.TransformPoint(new Vector3(h, 0f, 0f));
                platilloFinal.rotation = transform.rotation;
            }
        }

        private void ConstruirCapsulas()
        {
            if (contenedorCapsulas != null) Destruir(contenedorCapsulas.gameObject);
            var cont = new GameObject("Colisionadores_Guia").transform;
            cont.SetParent(transform, false);
            contenedorCapsulas = cont;
            capsulas.Clear();

            int n = Mathf.Max(1, Mathf.CeilToInt(largoTotal / largoCapsula));
            Vector3 prev = PuntoPorLargo(0f);
            for (int i = 1; i <= n; i++)
            {
                Vector3 sig = PuntoPorLargo(largoTotal * i / n);
                Vector3 d = sig - prev;
                var go = new GameObject("Capsula_" + i.ToString("000"));
                go.transform.SetParent(cont, false);
                go.transform.localPosition = (prev + sig) * 0.5f;
                go.transform.localRotation = Quaternion.LookRotation(d.normalized, Mathf.Abs(Vector3.Dot(d.normalized, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up);
                var cap = go.AddComponent<CapsuleCollider>();
                cap.direction = 2;
                cap.radius = Radio;
                cap.height = d.magnitude + 2f * Radio;   // las capsulas se solapan: cadena continua
                cap.sharedMaterial = materialFisico;
                capsulas.Add(cap);
                prev = sig;
            }
        }

        private Vector3 PuntoPorLargo(float s)
        {
            float f = Mathf.Clamp01(s / largoTotal) * (muestras.Length - 1);
            int i = Mathf.Min(Mathf.FloorToInt(f), muestras.Length - 2);
            return Vector3.Lerp(muestras[i], muestras[i + 1], f - i);
        }

        private static void Destruir(Object o)
        {
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        /// <summary>Malla tubular con marcos de transporte paralelo (sin torsion) y tapas.</summary>
        public static Mesh ConstruirTubo(Vector3[] c, float r, int lados)
        {
            int m = c.Length;
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            Vector3 t0 = (c[1] - c[0]).normalized;
            Vector3 nAnt = Vector3.Cross(t0, Mathf.Abs(t0.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            float v = 0f;
            for (int i = 0; i < m; i++)
            {
                Vector3 t = (c[Mathf.Min(i + 1, m - 1)] - c[Mathf.Max(i - 1, 0)]).normalized;
                Vector3 nn = (nAnt - t * Vector3.Dot(nAnt, t)).normalized;
                Vector3 b = Vector3.Cross(t, nn);
                nAnt = nn;
                if (i > 0) v += Vector3.Distance(c[i], c[i - 1]) / (2f * Mathf.PI * r);
                for (int k = 0; k <= lados; k++)
                {
                    float a = (float)k / lados * Mathf.PI * 2f;
                    Vector3 dir = nn * Mathf.Cos(a) + b * Mathf.Sin(a);
                    verts.Add(c[i] + dir * r);
                    norms.Add(dir);
                    uvs.Add(new Vector2((float)k / lados, v));
                }
            }
            int fila = lados + 1;
            for (int i = 0; i < m - 1; i++)
                for (int k = 0; k < lados; k++)
                {
                    int a = i * fila + k, b2 = a + fila;
                    tris.Add(a); tris.Add(a + 1); tris.Add(b2);
                    tris.Add(a + 1); tris.Add(b2 + 1); tris.Add(b2);
                }
            // tapas
            for (int e = 0; e < 2; e++)
            {
                int ia = e == 0 ? 0 : m - 1;
                Vector3 tn = e == 0 ? (c[0] - c[1]).normalized : (c[m - 1] - c[m - 2]).normalized;
                int centro = verts.Count;
                verts.Add(c[ia]); norms.Add(tn); uvs.Add(new Vector2(0.5f, 0.5f));
                int baseAnillo = verts.Count;
                for (int k = 0; k <= lados; k++)
                {
                    verts.Add(verts[ia * fila + k]); norms.Add(tn); uvs.Add(new Vector2((float)k / lados, 0f));
                }
                for (int k = 0; k < lados; k++)
                {
                    if (e == 0) { tris.Add(centro); tris.Add(baseAnillo + k + 1); tris.Add(baseAnillo + k); }
                    else { tris.Add(centro); tris.Add(baseAnillo + k); tris.Add(baseAnillo + k + 1); }
                }
            }
            var mesh = new Mesh();
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }

#if UNITY_EDITOR
        public static Mesh GuardarMalla(Mesh malla, string ruta)
        {
            string carpeta = System.IO.Path.GetDirectoryName(ruta).Replace('\\', '/');
            if (!UnityEditor.AssetDatabase.IsValidFolder(carpeta))
            {
                string[] partes = carpeta.Split('/');
                string acum = partes[0];
                for (int i = 1; i < partes.Length; i++)
                {
                    string sig = acum + "/" + partes[i];
                    if (!UnityEditor.AssetDatabase.IsValidFolder(sig)) UnityEditor.AssetDatabase.CreateFolder(acum, partes[i]);
                    acum = sig;
                }
            }
            var existente = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>(ruta);
            if (existente == null)
            {
                UnityEditor.AssetDatabase.CreateAsset(malla, ruta);
                existente = malla;
            }
            else
            {
                UnityEditor.EditorUtility.CopySerialized(malla, existente);
                existente.name = System.IO.Path.GetFileNameWithoutExtension(ruta);
                UnityEditor.EditorUtility.SetDirty(existente);
            }
            UnityEditor.AssetDatabase.SaveAssets();
            return existente;
        }
#endif

        private void OnDrawGizmosSelected()
        {
            if (muestras == null || muestras.Length < 2) return;
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.8f);
            Gizmos.matrix = transform.localToWorldMatrix;
            for (int i = 1; i < muestras.Length; i++) Gizmos.DrawLine(muestras[i - 1], muestras[i]);
        }
    }
}
