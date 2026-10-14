// ============================================================================
//  ProtesisPerfil.cs
//  Lee el perfil que genera calibrar_offset.py.
//
//  Es una lectura de archivo, no una conexion. La calibracion se hace una vez,
//  antes de la sesion, asi que no hay razon para un socket vivo con Python: si
//  el dia de la demo falla la webcam, el perfil anterior sigue estando y la
//  sesion arranca igual.
//
//  Se busca, por orden:
//    1. Assets/StreamingAssets/perfil_usuario.json   (versionado con el proyecto)
//    2. Application.persistentDataPath
//
//  Los campos de C# se llaman igual que las claves del JSON, guiones bajos
//  incluidos, porque JsonUtility mapea por nombre exacto.
// ============================================================================

using System;
using System.IO;
using UnityEngine;

[Serializable]
public class PerfilUsuario
{
    public string usuario;
    public string fecha;
    public string metodo;
    public string lado;                    // "derecho" | "izquierdo"

    public Vector3 offset_hombro_m;        // pivote del brazo  (entra en la cinematica)
    public Vector3 offset_pecho_m;         // IMU del torso     (solo visual)
    public Vector3 offset_imu_brazo_m;     // IMU del brazo     (solo visual)

    public float confianza;                // 0..1
    public float desvio_cm;

    public override string ToString() =>
        $"{usuario} ({fecha}, {metodo}, {lado})\n" +
        $"   hombro {offset_hombro_m}  pecho {offset_pecho_m}  brazo {offset_imu_brazo_m}\n" +
        $"   confianza {confianza:F2}, desvio {desvio_cm:F1} cm";
}

public static class ProtesisPerfil
{
    public const string FILENAME = "perfil_usuario.json";
    public const float CONFIANZA_MINIMA = 0.6f;

    /// <summary>Devuelve el perfil, o null si no hay archivo o esta roto.</summary>
    public static PerfilUsuario Cargar(out string rutaUsada)
    {
        rutaUsada = null;

        string[] candidatas =
        {
            Path.Combine(Application.streamingAssetsPath, FILENAME),
            Path.Combine(Application.persistentDataPath, FILENAME),
        };

        foreach (string ruta in candidatas)
        {
            try
            {
                if (!File.Exists(ruta)) continue;

                PerfilUsuario p = JsonUtility.FromJson<PerfilUsuario>(File.ReadAllText(ruta));

                if (p == null || p.offset_hombro_m == Vector3.zero)
                {
                    Debug.LogWarning($"[ProtesisPerfil] Perfil vacio o invalido en {ruta}");
                    continue;
                }

                rutaUsada = ruta;
                return p;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ProtesisPerfil] No pude leer {ruta}: {e.Message}");
            }
        }

        return null;
    }

    /// <summary>Chequeo de sensatez, los mismos rangos que valida el Python.</summary>
    public static bool EsPlausible(PerfilUsuario p, out string motivo)
    {
        motivo = null;

        float hx = Mathf.Abs(p.offset_hombro_m.x);
        float hy = Mathf.Abs(p.offset_hombro_m.y);

        if (hx < 0.10f || hx > 0.30f)
        {
            motivo = $"hombro lateral raro: {hx * 100f:F1} cm";
            return false;
        }
        if (hy < 0.12f || hy > 0.38f)
        {
            motivo = $"hombro vertical raro: {hy * 100f:F1} cm";
            return false;
        }
        if (p.offset_imu_brazo_m != Vector3.zero &&
            Mathf.Abs(p.offset_imu_brazo_m.y) <= hy)
        {
            motivo = "el punto del brazo no quedo por debajo del hombro";
            return false;
        }
        return true;
    }
}
