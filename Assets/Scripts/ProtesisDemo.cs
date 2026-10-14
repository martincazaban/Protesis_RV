// ============================================================================
//  ProtesisDemo.cs   -- v4
//  Panel de la sesion: estado del enlace, valores de las senales, barras
//  deslizantes para las senales escribibles y control de la comunicacion.
//
//  Las barras no mueven el brazo directamente: mandan el valor a la ESP32 y
//  el brazo se mueve con lo que el micro devuelve en el stream. Mientras el
//  valor devuelto no coincida con la barra, se vuelve a mandar.
// ============================================================================

using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(ProtesisLink))]
public class ProtesisDemo : MonoBehaviour
{
    [Tooltip("Maximo de envios por segundo para cada barra.")]
    public float envioHz = 20f;
    [Tooltip("Diferencia a partir de la cual se reenvia el valor de la barra.")]
    public float tolerancia = 0.05f;

    ProtesisLink link;
    Vector2 _scroll;

    readonly Dictionary<string, float> _barra = new Dictionary<string, float>();
    float _ultimoEnvio;

    void Awake()
    {
        link = GetComponent<ProtesisLink>();
        link.OnText += msg => Debug.Log($"Mensaje de la protesis: {msg}");
    }

    void Update()
    {
        if (!link.SchemaReady || !link.Connected) return;

        foreach (var d in link.Schema.sig)
        {
            if (d.w != 1) continue;

            float micro = link.Get(d.k);

            // La primera vez la barra arranca en el valor que tiene el micro
            if (!_barra.ContainsKey(d.k)) { _barra[d.k] = micro; continue; }

            if (Mathf.Abs(_barra[d.k] - micro) > tolerancia &&
                Time.unscaledTime - _ultimoEnvio >= 1f / envioHz)
            {
                link.SetSignal(d.k, _barra[d.k]);
                _ultimoEnvio = Time.unscaledTime;
            }
        }
    }

    void OnGUI()
    {
        GUILayout.BeginArea(new Rect(10, 10, 420, Screen.height - 20), GUI.skin.box);

        // --- estado del enlace ---
        GUI.color = link.Connected ? Color.green : Color.red;
        GUILayout.Label(link.Connected ? "CONECTADO" : "SIN DATOS");
        GUI.color = Color.white;
        GUILayout.Label($"ESP32: {(string.IsNullOrEmpty(link.espIp) ? "buscando..." : link.espIp)}");

        GUILayout.Space(6);

        // --- senales ---
        if (link.SchemaReady)
        {
            _scroll = GUILayout.BeginScrollView(_scroll);
            foreach (var d in link.Schema.sig)
            {
                float v = link.Get(d.k);

                if (d.w == 1 && _barra.ContainsKey(d.k))
                {
                    GUILayout.Label($"{d.l,-16} {_barra[d.k],7:F1} {d.u}   (micro: {v:F1})");
                    _barra[d.k] = GUILayout.HorizontalSlider(_barra[d.k], d.min, d.max);
                    GUILayout.Space(4);
                }
                else
                {
                    GUILayout.Label($"{d.l,-16} {v,9:F2} {d.u}");
                    Rect r = GUILayoutUtility.GetRect(380, 8);
                    GUI.HorizontalScrollbar(r, 0f, link.GetNormalized(d.k), 0f, 1f);
                }
            }
            GUILayout.EndScrollView();
        }
        else
        {
            GUILayout.Label("Esperando a la ESP32...");
            GUILayout.FlexibleSpace();
        }

        // --- comunicacion ---
        GUILayout.Space(6);
        GUILayout.Label("Comunicacion");
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Iniciar")) link.Send("START");
        if (GUILayout.Button("Detener")) link.Send("STOP");
        GUILayout.EndHorizontal();

        GUILayout.EndArea();
    }
}
