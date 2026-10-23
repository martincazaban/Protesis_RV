using System.Collections;
using System.Text;
using UnityEngine;

namespace Entrenamiento.Tests
{
    /// <summary>
    /// Prueba de respuesta del brazo fisico en espacio libre: aplica escalones de orientacion al PivoteBrazo
    /// (con el ProtesisArmDriver apagado) y mide sobrepaso, tiempo de asentamiento y error en rampa.
    /// Resultado en la propiedad estatica Resultado. Solo Play.
    /// </summary>
    public class PruebaRespuestaBrazo : MonoBehaviour
    {
        public static string Resultado = "";
        public static bool Terminado;

        private IEnumerator Start()
        {
            var bf = FindFirstObjectByType<BrazoFisico>();
            var drv = FindFirstObjectByType<ProtesisArmDriver>(); if (drv != null) drv.enabled = false;
            var piv = bf.pivoteObjetivo;
            var sb = new StringBuilder();
            yield return new WaitForSeconds(2.5f);   // espera el recentrado de sesion
            bf.manoObjetivo.SetElbowAngle(30f);
            piv.rotation = Quaternion.identity;
            yield return new WaitForSeconds(1.5f);

            Quaternion[] cand = {
                Quaternion.Euler(-120f, 0f, 0f), Quaternion.Euler(-100f, 40f, 0f), Quaternion.Euler(-110f, -50f, 10f), Quaternion.Euler(-135f, 20f, 0f), Quaternion.Euler(0f, 0f, 70f), Quaternion.Euler(-90f, 60f, 0f) };
            var destinos = new System.Collections.Generic.List<Quaternion>();
            foreach (var c in cand)
            {
                piv.rotation = c; Physics.SyncTransforms();
                if (bf.palmaObjetivo.position.y > 0.95f) destinos.Add(c); else sb.AppendLine("descartada (palma bajo 0.95 m): " + c.eulerAngles);
            }
            piv.rotation = Quaternion.Euler(-120f, 0f, 0f); yield return new WaitForSeconds(1.5f);
            foreach (var d in destinos)
            {
                float tot = Quaternion.Angle(bf.Cuerpo.rotation, d);
                piv.rotation = d;
                float maxSobre = 0f, tAsent = -1f, t = 0f, prev = tot;
                bool cruzo = false;
                while (t < 2f)
                {
                    yield return new WaitForFixedUpdate();
                    t += Time.fixedDeltaTime;
                    float e = Quaternion.Angle(bf.Cuerpo.rotation, d);
                    // sobrepaso: el error crece despues de haber bajado de 5 grados (rebote al otro lado)
                    if (e < 3f) cruzo = true;
                    if (cruzo) maxSobre = Mathf.Max(maxSobre, e);
                    if (tAsent < 0f && e < 1f) tAsent = t;
                    prev = e;
                }
                sb.AppendLine($"escalon {tot:F0} grados: asienta(<1 grado) en {(tAsent < 0 ? -1f : tAsent):F2} s, rebote max tras cruzar {maxSobre:F2} grados, error final {Quaternion.Angle(bf.Cuerpo.rotation, d):F2}");
            }

            // rampa: 60 grados/s sobre eje X durante 1.5 s
            piv.rotation = Quaternion.Euler(-110f, 0f, 0f); yield return new WaitForSeconds(1.5f);
            float maxErr = 0f, ang = 0f;
            for (float t = 0f; t < 1.5f; t += Time.fixedDeltaTime)
            {
                ang += 60f * Time.fixedDeltaTime;
                piv.rotation = Quaternion.Euler(-110f, ang, 0f);
                yield return new WaitForFixedUpdate();
                if (t > 0.3f) maxErr = Mathf.Max(maxErr, Quaternion.Angle(bf.Cuerpo.rotation, piv.rotation));
            }
            sb.AppendLine($"rampa 60 grados/s: error maximo en regimen {maxErr:F2} grados");

            // temblor tipo IMU: ruido de +-0.5 grados sobre una pose fija
            piv.rotation = Quaternion.Euler(-110f, 10f, 0f); yield return new WaitForSeconds(1.5f);
            Quaternion baseQ = piv.rotation; float maxV = 0f;
            for (float t = 0f; t < 1.5f; t += Time.fixedDeltaTime)
            {
                piv.rotation = baseQ * Quaternion.Euler(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f));
                yield return new WaitForFixedUpdate();
                maxV = Mathf.Max(maxV, bf.Cuerpo.angularVelocity.magnitude * Mathf.Rad2Deg);
            }
            sb.AppendLine($"ruido +-0.5 grados: velocidad angular maxima del cuerpo {maxV:F1} grados/s");
            Resultado = sb.ToString();
            Terminado = true;
        }
    }
}
