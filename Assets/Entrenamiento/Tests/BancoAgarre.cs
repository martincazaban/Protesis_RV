using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Entrenamiento.Tests
{
    /// <summary>
    /// Banco de pruebas automatico (solo Play) para comparar formas de collider de la zona de agarre
    /// de la palma y modos de pre-agarre. Para cada configuracion repite varios intentos sobre
    /// distintos cubos: acerca la palma desde arriba, cierra los dedos de a poco, levanta y suelta.
    /// Mide: exito, curl al agarrar, empuje previo del cubo, distancia palma-cubo, perturbacion de
    /// los otros cubos, fugas de IgnoreCollision. No modifica la escena guardada.
    /// </summary>
    public class BancoAgarre : MonoBehaviour
    {
        public float escalaTiempo = 4f;
        public int pruebasPorConfig = 8;
        [Tooltip("Subcadena para filtrar configuraciones (vacio = todas).")]
        public string filtro = "";
        public float velocidadDescenso = 0.03f;
        public float velocidadCierre = 0.35f;
        [Tooltip("Multiplicador del desplazamiento lateral aleatorio de la palma respecto del cubo (1 = +-0.8 cm).")]
        public float jitterEscala = 1f;
        [Tooltip("Altura minima (m) del origen de la palma sobre el centro del cubo al terminar el descenso.")]
        public float hMinimo = 0.022f;

        public static string Resultado = "";
        public static bool Terminado = false;
        public static string Progreso = "";

        class Cfg
        {
            public string nombre, tipo;
            public float a, b, c;                       // dimensiones (m)
            public float offN, offF, offA;              // desplazamiento: normal de palma, hacia dedos, a lo ancho
            public PalmGrabber.ModoPreAgarre modo;
        }

        class Res
        {
            public bool ok, agarro, equivocado, enRango50, liftOk, soltoOk;
            public float hActiva = -1f; public float curlAgarre = -1f, curlSuelta = -1f, empuje50 = 0f, distAgarre = 0f, desvioLevante = 0f, maxOtros = 0f;
            public int fugas;
        }

        PalmGrabber palm; HandController hand; Transform piv; Vector3 hombro;
        Collider capOriginal; GameObject zonaActual;
        Grabbable[] cubos; Vector3[] pos0;
        float[] distTab; Vector3[] p0Tab, fwd0Tab; int eActual = -1;
        Collider[] handCols;

        readonly StringBuilder log = new StringBuilder();
        void L(string s) { log.AppendLine(s); Debug.Log("[Banco] " + s); }

        List<Cfg> Configs()
        {
            var S = PalmGrabber.ModoPreAgarre.DedosSiempre; var SB = PalmGrabber.ModoPreAgarre.DedosSiempreYBrazoEnRango; var N = PalmGrabber.ModoPreAgarre.Ninguno; var D = PalmGrabber.ModoPreAgarre.IgnorarDedos; var B = PalmGrabber.ModoPreAgarre.IgnorarBrazoCompleto;
            var l = new List<Cfg>
            {
                new Cfg{nombre="00 ESCENA-GUARDADA (esfera r3 + dedos-siempre-fantasma)", tipo="nada", modo=N},
                new Cfg{nombre="01 Capsula-original | solido",            tipo="orig", modo=N},
                new Cfg{nombre="02 Capsula-original | dedos-fantasma",    tipo="orig", modo=D},
                new Cfg{nombre="03 Esfera r3 | solido",                   tipo="esfera", a=0.03f, offN=0.015f, offF=0.01f, modo=N},
                new Cfg{nombre="04 Esfera r3 | dedos-fantasma",           tipo="esfera", a=0.03f, offN=0.015f, offF=0.01f, modo=D},
                new Cfg{nombre="05 Esfera r4 | solido",                   tipo="esfera", a=0.04f, offN=0.015f, offF=0.01f, modo=N},
                new Cfg{nombre="06 Esfera r4 | dedos-fantasma",           tipo="esfera", a=0.04f, offN=0.015f, offF=0.01f, modo=D},
                new Cfg{nombre="07 Caja 8x9x3.5 | solido",                tipo="caja", a=0.08f, b=0.09f, c=0.035f, offN=0.0075f, offF=0.01f, modo=N},
                new Cfg{nombre="08 Caja 8x9x3.5 | dedos-fantasma",        tipo="caja", a=0.08f, b=0.09f, c=0.035f, offN=0.0075f, offF=0.01f, modo=D},
                new Cfg{nombre="09 Caja 8x9x5 | solido",                  tipo="caja", a=0.08f, b=0.09f, c=0.05f, offN=0.015f, offF=0.01f, modo=N},
                new Cfg{nombre="10 Caja 8x9x5 | dedos-fantasma",          tipo="caja", a=0.08f, b=0.09f, c=0.05f, offN=0.015f, offF=0.01f, modo=D},
                new Cfg{nombre="11 Caja 7x8x4 | dedos-fantasma",          tipo="caja", a=0.07f, b=0.08f, c=0.04f, offN=0.01f, offF=0.01f, modo=D},
                new Cfg{nombre="12 Capsula r3 h10 (eje dedos) | solido",  tipo="capsulaG", a=0.03f, b=0.10f, offN=0.015f, offF=0.02f, modo=N},
                new Cfg{nombre="13 Capsula r3 h10 (eje dedos) | dedos-fantasma", tipo="capsulaG", a=0.03f, b=0.10f, offN=0.015f, offF=0.02f, modo=D},
                new Cfg{nombre="14 Caja 7x8x4 | dedos-siempre-fantasma",  tipo="caja", a=0.07f, b=0.08f, c=0.04f, offN=0.01f, offF=0.01f, modo=S},
                new Cfg{nombre="15 Caja 7x8x4 | dedos-siempre + brazo-en-rango", tipo="caja", a=0.07f, b=0.08f, c=0.04f, offN=0.01f, offF=0.01f, modo=SB},
                new Cfg{nombre="16 Capsula-original | dedos-siempre-fantasma", tipo="orig", modo=S},
                new Cfg{nombre="17 Esfera r3 | dedos-siempre-fantasma",      tipo="esfera", a=0.03f, offN=0.015f, offF=0.01f, modo=S},
            };
            if (!string.IsNullOrEmpty(filtro)) { var fs = filtro.Split('|'); l = l.Where(c => fs.Any(f => c.nombre.StartsWith(f))).ToList(); }
            return l;
        }

        IEnumerator Start()
        {
            Time.timeScale = escalaTiempo;
            palm = FindObjectOfType<PalmGrabber>(); hand = palm.hand; piv = hand.transform.parent; hombro = piv.position;
            capOriginal = palm.GetComponent<CapsuleCollider>();
            handCols = hand.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger).ToArray();
            cubos = FindObjectsOfType<Grabbable>().OrderBy(g => g.name).ToArray();
            pos0 = cubos.Select(c => c.transform.position).ToArray();
            ConstruirTabla();
            L($"cubos={cubos.Length} grabCurl={palm.grabCurl} hist={palm.histeresis} ventana={palm.ventanaAgarre} escalaTiempo={escalaTiempo}");

            var objetivos = new[] { 1, 2, 5, 6, 9, 10, 4, 8 };
            var jit = new[] { new Vector2(0f, 0f), new Vector2(0.008f, -0.006f), new Vector2(-0.007f, 0.008f), new Vector2(0.005f, 0.006f), new Vector2(-0.008f, -0.005f), new Vector2(0f, 0.008f), new Vector2(-0.004f, 0.003f), new Vector2(0.006f, -0.008f) };

            var resumen = new StringBuilder();
            resumen.AppendLine("CONFIGURACION | exitos | curl medio agarre | empuje previo (cm) | dist palma-cubo (cm) | max perturb. otros (cm) | fugas | equivocados | sin rango a 50% | altura de activacion sobre contacto (cm)");
            var cfgs = Configs();
            int ci = 0;
            foreach (var cfg in cfgs)
            {
                ci++;
                Progreso = $"{ci}/{cfgs.Count} {cfg.nombre}";
                IrAInicio(objetivos[0], 0.12f);
                yield return new WaitForSeconds(0.5f);
                AplicarConfig(cfg);
                yield return new WaitForSeconds(0.4f);

                var rs = new List<Res>();
                for (int k = 0; k < pruebasPorConfig; k++)
                {
                    int idx = objetivos[k % objetivos.Length];
                    var r = new Res();
                    yield return StartCoroutine(Prueba(idx, jit[k % jit.Length], r));
                    rs.Add(r);
                    L($"  {cfg.nombre} #{k + 1} cubo={cubos[idx].name} ok={r.ok} agarro={r.agarro} curl={r.curlAgarre:F2} hAct={r.hActiva * 100f:F1}cm enRango@50={r.enRango50} empuje@50={r.empuje50 * 100f:F1}cm dist={r.distAgarre * 100f:F1}cm desvio={r.desvioLevante * 100f:F1}cm solto@{r.curlSuelta:F2} otros={r.maxOtros * 100f:F1}cm fugas={r.fugas} equivocado={r.equivocado}");
                }
                int ok = rs.Count(r => r.ok);
                var ag = rs.Where(r => r.agarro).ToList();
                string linea = $"{cfg.nombre} | {ok}/{rs.Count} | {(ag.Count > 0 ? ag.Average(r => r.curlAgarre).ToString("F2") : "-")} | {rs.Average(r => r.empuje50) * 100f:F1} | {(ag.Count > 0 ? (ag.Average(r => r.distAgarre) * 100f).ToString("F1") : "-")} | {rs.Max(r => r.maxOtros) * 100f:F1} | {rs.Sum(r => r.fugas)} | {rs.Count(r => r.equivocado)} | {rs.Count(r => !r.enRango50)} | {(rs.Any(r => r.hActiva >= 0f) ? (rs.Where(r => r.hActiva >= 0f).Average(r => r.hActiva) * 100f).ToString("F1") : "-")}";
                resumen.AppendLine(linea);
                L("RESUMEN " + linea);
            }
            Time.timeScale = 1f;
            Resultado = resumen.ToString();
            try
            {
                var dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Logs_Claude"));
                System.IO.Directory.CreateDirectory(dir);
                System.IO.File.WriteAllText(System.IO.Path.Combine(dir, "banco_agarre.txt"), Resultado + "\n\n" + log);
            }
            catch (Exception e) { Debug.LogWarning(e.Message); }
            Terminado = true;
            L("FIN BANCO");
        }

        // ------------------------------------------------------------------
        //  Configuracion de la zona de agarre
        // ------------------------------------------------------------------
        void AplicarConfig(Cfg c)
        {
            if (c.tipo == "nada") { L($"CONFIG {c.nombre} (escena tal cual, modo={palm.modoPreAgarre})"); return; }
            if (zonaActual != null) { Destroy(zonaActual); zonaActual = null; }
            capOriginal.enabled = (c.tipo == "orig");
            palm.SetModo(c.modo);
            if (c.tipo == "orig") { L($"CONFIG {c.nombre}"); return; }

            var go = new GameObject("ZonaTest");
            go.transform.SetParent(palm.transform, false);
            var ls = palm.transform.lossyScale;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = new Vector3(1f / ls.x, 1f / ls.y, 1f / ls.z);
            // ejes de la palma: x = a lo ancho, -y = hacia las puntas de los dedos, -z = hacia el lado de agarre (normal)
            go.transform.localPosition = new Vector3(c.offA / ls.x, -c.offF / ls.y, -c.offN / ls.z);
            Collider col = null;
            switch (c.tipo)
            {
                case "esfera": { var s = go.AddComponent<SphereCollider>(); s.radius = c.a; col = s; break; }
                case "caja": { var b = go.AddComponent<BoxCollider>(); b.size = new Vector3(c.a, c.b, c.c); col = b; break; }
                case "capsulaG": { var k = go.AddComponent<CapsuleCollider>(); k.direction = 1; k.radius = c.a; k.height = c.b; col = k; break; }
                case "hull":
                    {
                        var conn = hand.GetComponentsInChildren<MeshCollider>(true).First(m => m.name.StartsWith("Palm _"));
                        go.transform.position = conn.transform.position;
                        go.transform.rotation = conn.transform.rotation;
                        var cs = conn.transform.lossyScale;
                        go.transform.localScale = new Vector3(cs.x / ls.x, cs.y / ls.y, cs.z / ls.z);
                        var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = conn.sharedMesh; mc.convex = true; col = mc; break;
                    }
            }
            col.isTrigger = true;
            zonaActual = go;
            Physics.SyncTransforms();
            L($"CONFIG {c.nombre}  bounds(mundo)={col.bounds.size.ToString("F3")} centro={col.bounds.center.ToString("F3")} palma={palm.transform.position.ToString("F3")}");
        }

        // ------------------------------------------------------------------
        //  Una prueba
        // ------------------------------------------------------------------
        void ResetCubos()
        {
            for (int i = 0; i < cubos.Length; i++)
            {
                var rb = cubos[i].rb;
                rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
                rb.position = pos0[i]; rb.rotation = Quaternion.identity;
                cubos[i].transform.SetPositionAndRotation(pos0[i], Quaternion.identity);
                rb.WakeUp();
            }
        }

        int Fugas()
        {
            int n = 0;
            foreach (var h in handCols) { if (h == null || PalmGrabber.EsDedoFantasma(h)) continue; foreach (var g in cubos) foreach (var c in g.GetComponentsInChildren<Collider>()) if (Physics.GetIgnoreCollision(h, c)) n++; }
            return n;
        }

        public string fase = ""; readonly HashSet<int> volo = new HashSet<int>();
        float MaxOtros(int idx)
        {
            float m = 0f;
            for (int i = 0; i < cubos.Length; i++)
            {
                if (i == idx) continue;
                float d = (cubos[i].transform.position - pos0[i]).magnitude;
                if (d > 0.15f && volo.Add(i * 1000 + idx)) L($"    VUELO cubo={cubos[i].name} (objetivo={cubos[idx].name}) fase={fase} d={d * 100f:F0}cm pos={cubos[i].transform.position:F2} v={cubos[i].rb.linearVelocity.magnitude:F1} palma={palm.transform.position:F3}");
                m = Mathf.Max(m, d);
            }
            return m;
        }

        void IrAInicio(int idx, float alto)
        {
            hand.SetHandCurl(0f);
            Mover(pos0[idx] + Vector3.up * alto + new Vector3(0f, 0f, 0f));
            Physics.SyncTransforms();
        }

        IEnumerator Prueba(int idx, Vector2 jit, Res r)
        {
            var g = cubos[idx];
            float alto = 0.12f;
            Vector3 baseP = pos0[idx] + new Vector3(jit.x, 0f, jit.y) * jitterEscala;
            hand.SetHandCurl(0f);
            Mover(baseP + Vector3.up * alto);
            yield return new WaitForSeconds(0.5f);
            ResetCubos();
            yield return new WaitForSeconds(0.5f);
            // el reset de cubos con la mano arriba no debe dejar nada ignorado
            r.fugas = Fugas();
            fase = "reset"; MaxOtros(idx);

            fase = "descenso";
            // --- descenso ---
            float h = alto, hMin = hMinimo;
            while (h > hMin)
            {
                h -= velocidadDescenso * Time.fixedDeltaTime;
                if (r.hActiva < 0f && g.EstadoActual == Grabbable.Estado.EnRango) r.hActiva = h - hMin;
                Mover(baseP + Vector3.up * h);
                r.maxOtros = Mathf.Max(r.maxOtros, MaxOtros(idx));
                yield return new WaitForFixedUpdate();
            }
            if (r.hActiva < 0f && g.EstadoActual == Grabbable.Estado.EnRango) r.hActiva = 0f;
            for (float t = 0f; t < 0.3f; t += Time.fixedDeltaTime) { Mover(baseP + Vector3.up * h); r.maxOtros = Mathf.Max(r.maxOtros, MaxOtros(idx)); yield return new WaitForFixedUpdate(); }

            fase = "cierre";
            // --- cierre ---
            float curl = 0f; bool marcado50 = false;
            while (curl < 1f)
            {
                curl = Mathf.Min(1f, curl + velocidadCierre * Time.fixedDeltaTime);
                hand.SetHandCurl(curl);
                Mover(baseP + Vector3.up * h);
                yield return new WaitForFixedUpdate();
                r.maxOtros = Mathf.Max(r.maxOtros, MaxOtros(idx));
                if (!marcado50 && curl >= 0.5f) { marcado50 = true; r.enRango50 = g.EstadoActual != Grabbable.Estado.FueraDeRango; r.empuje50 = (g.transform.position - pos0[idx]).magnitude; }
                if (!r.agarro && palm.Held != null)
                {
                    r.agarro = true; r.curlAgarre = curl; r.equivocado = palm.Held != g;
                    r.distAgarre = (palm.Held.transform.position - palm.transform.position).magnitude;
                    if (!marcado50) { marcado50 = true; r.enRango50 = true; r.empuje50 = (g.transform.position - pos0[idx]).magnitude; }
                }
                if (r.agarro && curl > r.curlAgarre + 0.12f) break;
            }
            if (!marcado50) r.enRango50 = g.EstadoActual != Grabbable.Estado.FueraDeRango;

            if (r.agarro)
            {
                var held = palm.Held;
                fase = "levantar";
                // --- levantar 5 cm ---
                yield return new WaitForSeconds(0.2f);
                Vector3 off0 = held.transform.position - palm.transform.position;
                float lift = 0f;
                while (lift < 0.05f) { lift += 0.04f * Time.fixedDeltaTime; Mover(baseP + Vector3.up * (h + lift)); yield return new WaitForFixedUpdate(); }
                yield return new WaitForSeconds(0.3f);
                r.desvioLevante = ((held.transform.position - palm.transform.position) - off0).magnitude;
                r.liftOk = palm.Held == held && r.desvioLevante < 0.015f && held.transform.position.y > pos0[idx].y + 0.03f;

                // --- soltar ---
                float c2 = curl;
                while (c2 > 0f && palm.Held != null) { c2 = Mathf.Max(0f, c2 - 0.8f * Time.fixedDeltaTime); hand.SetHandCurl(c2); yield return new WaitForFixedUpdate(); }
                r.curlSuelta = c2; r.soltoOk = palm.Held == null;
                hand.SetHandCurl(0f);
                for (int i = 0; i < 30; i++) { lift += 0.1f * Time.fixedDeltaTime; Mover(baseP + Vector3.up * (h + lift)); yield return new WaitForFixedUpdate(); }
                yield return new WaitForSeconds(1.3f);
                r.fugas += Fugas();
            }
            r.ok = r.agarro && !r.equivocado && r.curlAgarre >= 0.5f && r.curlAgarre <= 0.65f && r.liftOk && r.soltoOk && r.fugas == 0;
            hand.SetHandCurl(0f);
            Mover(baseP + Vector3.up * alto);
            yield return new WaitForSeconds(0.5f);
        }

        // ------------------------------------------------------------------
        //  Cinematica del brazo simulado (codo por tabla + rotacion del pivote, palma hacia abajo)
        // ------------------------------------------------------------------
        void ConstruirTabla()
        {
            int N = 181;
            distTab = new float[N]; p0Tab = new Vector3[N]; fwd0Tab = new Vector3[N];
            piv.rotation = Quaternion.identity;
            for (int i = 0; i < N; i++)
            {
                hand.SetElbowAngle(i * 0.5f); Physics.SyncTransforms();
                p0Tab[i] = palm.transform.position - hombro; fwd0Tab[i] = palm.transform.forward; distTab[i] = p0Tab[i].magnitude;
            }
            eActual = -1;
        }

        void Mover(Vector3 t)
        {
            Vector3 d = t - hombro; float need = d.magnitude;
            int bi = 0; float be = 1e9f;
            for (int i = 0; i < distTab.Length; i++) { float e = Mathf.Abs(distTab[i] - need); if (e < be) { be = e; bi = i; } }
            if (bi != eActual) { hand.SetElbowAngle(bi * 0.5f); eActual = bi; }
            Quaternion r1 = Quaternion.FromToRotation(p0Tab[bi], d);
            Vector3 eje = d.normalized; float best = -2f; Quaternion bq = r1;
            for (float phi = 0f; phi < 360f; phi += 3f)
            {
                var q = Quaternion.AngleAxis(phi, eje) * r1;
                float sc = (q * fwd0Tab[bi]).y;          // la normal de la palma es -forward: queremos que mire hacia abajo
                if (sc > best) { best = sc; bq = q; }
            }
            piv.rotation = bq;
        }
    }
}
