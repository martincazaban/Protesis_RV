// ============================================================================
//  ProtesisLink.cs   -- v3
//  Cliente UDP para protesis_esp32.ino
//
//  Poner en un GameObject vacio. Desde cualquier script:
//      float codo = link.Get("ang_elbow");
//      if (link.SchemaReady) { ... }
//
//  El diccionario de senales se arma solo con el JSON que manda la ESP32. Si
//  reflasheas el micro con la tabla cambiada, el schemaId no coincide, se
//  descartan los paquetes viejos y se vuelve a pedir el esquema.
//
//  ARREGLO RESPECTO DE LA VERSION ANTERIOR
//  Time.unscaledTime se leia desde el hilo de red, y la API de Unity solo se
//  puede tocar desde el hilo principal. Ademas de llenar la consola, hacia que
//  "Connected" nunca se actualizara: el overlay decia SIN DATOS mientras
//  recibia 100 paquetes por segundo. Ahora el hilo de red solo levanta una
//  bandera y el sello de tiempo se pone en Update().
//
//  SENALES ESCRIBIBLES (v5)
//  Las senales con w=1 en el esquema se fijan desde Unity con SetSignal(),
//  que manda "SET <key> <valor>". El micro guarda el valor y lo devuelve en
//  cada paquete de datos.
//
//  DESCUBRIMIENTO (v4)
//  En Windows el broadcast global 255.255.255.255 sale por UNA sola interfaz,
//  que no siempre es la del WiFi (adaptadores virtuales, VPN, Ethernet). La
//  ESP32 ni se enteraba: el monitor serie no mostraba "[RX] DISCOVER". Ahora se
//  prueba, en orden:
//    1. la ultima IP donde se encontro la ESP32 (queda guardada entre sesiones)
//    2. broadcast global + broadcast dirigido de cada subred local (x.x.x.255)
//    3. si nada contesta, DISCOVER unicast a cada host de las subredes locales.
//       Las respuestas a paquetes unicast pasan el firewall sin reglas extra.
//  La IP de la ESP32 puede cambiar (DHCP); no hace falta cargarla a mano.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

[Serializable]
public class SignalDef
{
    public string k;      // key
    public string l;      // label
    public string u;      // unidad
    public float min;
    public float max;
    public int w;         // 1 = se puede fijar desde Unity con SET
}

[Serializable]
public class SchemaMsg
{
    public int id;        // hash de la tabla
    public int n;
    public int rate;
    public SignalDef[] sig;
}

public class ProtesisLink : MonoBehaviour
{
    [Header("Red")]
    [Tooltip("Vacio = descubrir la ESP32 por broadcast.")]
    public string espIp = "";
    public int espPort = 4210;
    public int localPort = 4211;

    [Header("Descubrimiento")]
    [Tooltip("Si broadcast no contesta, mandar DISCOVER a cada host de la subred.")]
    public bool barrerSubred = true;
    [Tooltip("Recordar la ultima IP de la ESP32 entre sesiones.")]
    public bool recordarIp = true;

    [Header("Stream")]
    public int rateHz = 100;
    public bool autoStart = true;

    [Header("Debug")]
    public bool logText = true;

    // ---- API publica ----
    public bool Connected { get; private set; }
    public bool SchemaReady { get; private set; }
    public SchemaMsg Schema { get; private set; }
    public int PacketsReceived { get; private set; }
    public int PacketsDropped { get; private set; }
    public float PacketsPerSecond { get; private set; }
    public uint TimestampMs { get { lock (_lock) return _lastTimestampMs; } }

    public event Action OnSchemaReady;
    public event Action<string> OnText;

    // ---- interno ----
    const byte TYPE_TEXT = 0x01;
    const byte TYPE_DATA = 0x02;
    const byte TYPE_SCHEMA = 0x03;

    UdpClient _udp;
    Thread _rx;
    Thread _keepAlive;                 // PING desde hilo propio: no depende de los tirones del editor
    volatile bool _detenidoManual;     // true si el usuario pidio STOP (no reanudar solo)
    float _ultimaReanudacion;
    volatile bool _running;
    volatile bool _gotPacket;          // bandera: el hilo de red no toca Time

    IPEndPoint _esp;
    readonly object _lock = new object();
    float[] _values = new float[0];
    uint _lastTimestampMs;
    volatile int _rxSchemaId = -1;

    readonly Dictionary<string, int> _index = new Dictionary<string, int>();
    readonly Queue<string> _textQueue = new Queue<string>();
    string _pendingSchemaJson;

    float _lastRxTime;
    float _lastPingTime;
    float _lastHandshakeTime;
    float _ppsWindowStart;
    int _ppsCounter;
    int _intentosDiscover;
    List<IPAddress> _broadcasts;
    List<IPAddress> _hostsSubred;
    const string PREF_ULTIMA_IP = "ProtesisLink.ultimaIp";

    // ======================= CICLO DE VIDA ==================================
    void Start()
    {
        try
        {
            _udp = new UdpClient(localPort);
            _udp.EnableBroadcast = true;
            _udp.Client.ReceiveTimeout = 400;
        }
        catch (SocketException e)
        {
            Debug.LogError($"[ProtesisLink] No pude abrir el puerto {localPort}: {e.Message}");
            enabled = false;
            return;
        }

        _running = true;
        _rx = new Thread(RxLoop) { IsBackground = true };
        _rx.Start();

        _keepAlive = new Thread(KeepAliveLoop) { IsBackground = true };
        _keepAlive.Start();

        if (!string.IsNullOrEmpty(espIp))
            _esp = new IPEndPoint(IPAddress.Parse(espIp), espPort);

        Handshake();
    }

    void OnDestroy()
    {
        _running = false;
        if (_esp != null) Send("STOP");
        try { _udp?.Close(); } catch { }
        if (_rx != null && _rx.IsAlive) _rx.Join(600);
        if (_keepAlive != null && _keepAlive.IsAlive) _keepAlive.Join(600);
    }

    void Update()
    {
        if (_gotPacket)
        {
            _gotPacket = false;
            _lastRxTime = Time.unscaledTime;
        }

        if (_pendingSchemaJson != null)
        {
            string json;
            lock (_lock) { json = _pendingSchemaJson; _pendingSchemaJson = null; }
            ApplySchema(json);
        }

        while (true)
        {
            string msg = null;
            lock (_lock) { if (_textQueue.Count > 0) msg = _textQueue.Dequeue(); }
            if (msg == null) break;

            if (logText) Debug.Log($"[ProtesisLink] TEXT: {msg}");
            OnText?.Invoke(msg);

            if (msg.StartsWith("PROTESIS ") && _esp == null)
            {
                espIp = msg.Substring(9).Trim();
                _esp = new IPEndPoint(IPAddress.Parse(espIp), espPort);
                if (recordarIp) { PlayerPrefs.SetString(PREF_ULTIMA_IP, espIp); PlayerPrefs.Save(); }
                Debug.Log($"[ProtesisLink] ESP32 encontrada en {espIp} " +
                          $"(intento de descubrimiento {_intentosDiscover})");
                Handshake();
            }
        }

        bool mismatch = SchemaReady && _rxSchemaId >= 0 && _rxSchemaId != Schema.id;
        if ((!SchemaReady || mismatch) && Time.time - _lastHandshakeTime > 1.5f)
        {
            if (mismatch)
                Debug.LogWarning($"[ProtesisLink] Esquema desactualizado " +
                                 $"(micro={_rxSchemaId}, local={Schema.id}). Resincronizando.");
            Handshake();
        }

        // Reanudacion automatica: si la ESP32 ya se conocia pero el stream se corto
        // (timeout del micro por un tiron del editor, wifi, etc.), se vuelve a pedir START.
        if (_esp != null && SchemaReady && !_detenidoManual &&
            Time.unscaledTime - _lastRxTime > 1f &&
            Time.unscaledTime - _ultimaReanudacion > 1.5f)
        {
            _ultimaReanudacion = Time.unscaledTime;
            Send("HELLO");
            Send($"RATE {rateHz}");
            Send("START");
        }

        Connected = (Time.unscaledTime - _lastRxTime) < 2f;

        if (Time.time - _ppsWindowStart >= 1f)
        {
            PacketsPerSecond = _ppsCounter / (Time.time - _ppsWindowStart);
            _ppsCounter = 0;
            _ppsWindowStart = Time.time;
        }
    }

    // ========================= HANDSHAKE ====================================
    void Handshake()
    {
        _lastHandshakeTime = Time.time;

        if (_esp == null)
        {
            Descubrir();
            return;
        }

        Send("HELLO");
        Send("SCHEMA");
        Send($"RATE {rateHz}");
        if (autoStart) Send("START");
    }

    // ======================= DESCUBRIMIENTO =================================
    void Descubrir()
    {
        _intentosDiscover++;
        if (_broadcasts == null) ArmarDestinos();

        // 1. Ultima IP conocida
        if (recordarIp)
        {
            string ultima = PlayerPrefs.GetString(PREF_ULTIMA_IP, "");
            if (IPAddress.TryParse(ultima, out IPAddress ipUlt))
                SendRaw("DISCOVER", new IPEndPoint(ipUlt, espPort));
        }

        // 2. Broadcast global y dirigido por subred
        SendRaw("DISCOVER", new IPEndPoint(IPAddress.Broadcast, espPort));
        foreach (var b in _broadcasts)
            SendRaw("DISCOVER", new IPEndPoint(b, espPort));

        // 3. Barrido unicast, a partir del segundo intento sin respuesta
        if (barrerSubred && _intentosDiscover >= 2 && _hostsSubred.Count > 0)
        {
            if (_intentosDiscover == 2)
                Debug.Log($"[ProtesisLink] Broadcast sin respuesta. Barriendo " +
                          $"{_hostsSubred.Count} hosts de la subred local.");
            foreach (var h in _hostsSubred)
                SendRaw("DISCOVER", new IPEndPoint(h, espPort), false);
        }
    }

    /// <summary>Broadcast dirigido y lista de hosts de cada interfaz IPv4
    /// activa (sin loopback). Subredes de mas de 1024 hosts no se barren.</summary>
    void ArmarDestinos()
    {
        _broadcasts = new List<IPAddress>();
        _hostsSubred = new List<IPAddress>();
        var log = new StringBuilder("[ProtesisLink] Interfaces para descubrir:");

        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (var ua in ni.GetIPProperties().UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    byte[] ip = ua.Address.GetAddressBytes();
                    if (ip[0] == 169 && ip[1] == 254) continue;          // link-local

                    byte[] mask = (ua.IPv4Mask != null && !ua.IPv4Mask.Equals(IPAddress.Any))
                                  ? ua.IPv4Mask.GetAddressBytes()
                                  : new byte[] { 255, 255, 255, 0 };     // si no hay dato, /24

                    uint uip = ToUInt(ip), umask = ToUInt(mask);
                    uint red = uip & umask, bcast = red | ~umask;
                    _broadcasts.Add(FromUInt(bcast));
                    log.Append($"\n   {ni.Name}: {ua.Address} -> {FromUInt(bcast)}");

                    uint hosts = ~umask - 1;
                    if (hosts <= 1024)
                        for (uint h = red + 1; h < bcast; h++)
                            if (h != uip) _hostsSubred.Add(FromUInt(h));
                }
            }
        }
        catch (Exception e)
        {
            log.Append($"\n   (no pude listar interfaces: {e.Message})");
        }

        if (logText) Debug.Log(log.ToString());
    }

    static uint ToUInt(byte[] b) => ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
    static IPAddress FromUInt(uint v) =>
        new IPAddress(new[] { (byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v });

public void Send(string cmd)
    {
        if (_esp == null) return;
        if (cmd == "STOP") _detenidoManual = true;
        else if (cmd == "START") _detenidoManual = false;
        SendRaw(cmd, _esp);
    }

    void KeepAliveLoop()
    {
        while (_running)
        {
            Thread.Sleep(500);
            var esp = _esp;
            if (esp != null && _running) SendRaw("PING", esp, false);
        }
    }

    void SendRaw(string cmd, IPEndPoint target, bool avisarError = true)
    {
        try
        {
            byte[] b = Encoding.ASCII.GetBytes(cmd);
            _udp.Send(b, b.Length, target);
        }
        catch (Exception e)
        {
            if (avisarError)
                Debug.LogWarning($"[ProtesisLink] Error al enviar '{cmd}' a {target}: {e.Message}");
        }
    }

    // ========================= THREAD DE RED ================================
    void RxLoop()
    {
        IPEndPoint any = new IPEndPoint(IPAddress.Any, 0);
        while (_running)
        {
            try
            {
                byte[] data = _udp.Receive(ref any);
                Parse(data);
            }
            catch (SocketException) { if (!_running) break; }
            catch (ObjectDisposedException) { break; }
            catch (Exception e) { Debug.LogWarning($"[ProtesisLink] RX: {e.Message}"); }
        }
    }

    void Parse(byte[] b)
    {
        if (b.Length < 8) return;
        if (b[0] != (byte)'P' || b[1] != (byte)'R') return;

        byte type = b[3];
        int p = 8;

        switch (type)
        {
            case TYPE_TEXT:
                {
                    string s = Encoding.ASCII.GetString(b, p, b.Length - p);
                    lock (_lock) _textQueue.Enqueue(s);
                    break;
                }

            case TYPE_SCHEMA:
                {
                    string json = Encoding.ASCII.GetString(b, p, b.Length - p);
                    lock (_lock) _pendingSchemaJson = json;   // JsonUtility va en main thread
                    break;
                }

            case TYPE_DATA:
                {
                    if (b.Length < p + 7) return;
                    int sid = BitConverter.ToUInt16(b, p); p += 2;
                    uint tms = BitConverter.ToUInt32(b, p); p += 4;
                    int n = b[p]; p += 1;
                    if (b.Length < p + n * 4) return;

                    _rxSchemaId = sid;

                    // El paquete es posicional: si la tabla del micro cambio,
                    // los indices ya no significan lo mismo. Se descarta.
                    if (SchemaReady && sid != Schema.id) { PacketsDropped++; return; }

                    lock (_lock)
                    {
                        if (_values.Length != n) _values = new float[n];
                        for (int i = 0; i < n; i++)
                            _values[i] = BitConverter.ToSingle(b, p + i * 4);
                        _lastTimestampMs = tms;
                    }

                    PacketsReceived++;
                    _ppsCounter++;
                    _gotPacket = true;
                    break;
                }
        }
    }

    // ========================= ESQUEMA ======================================
    void ApplySchema(string json)
    {
        try
        {
            SchemaMsg s = JsonUtility.FromJson<SchemaMsg>(json);
            if (s == null || s.sig == null) return;

            _index.Clear();
            for (int i = 0; i < s.sig.Length; i++)
                _index[s.sig[i].k] = i;

            Schema = s;
            SchemaReady = true;
            PacketsDropped = 0;

            var sb = new StringBuilder();
            sb.Append($"[ProtesisLink] Esquema id={s.id}: {s.sig.Length} senales @ {s.rate} Hz\n");
            foreach (var d in s.sig)
                sb.Append($"   {d.k,-10} {d.l}  [{d.min}..{d.max} {d.u}]\n");
            Debug.Log(sb.ToString());

            OnSchemaReady?.Invoke();
        }
        catch (Exception e)
        {
            Debug.LogError($"[ProtesisLink] Esquema invalido: {e.Message}\n{json}");
        }
    }

    // ========================= ACCESO A DATOS ===============================
    public bool Has(string key) => _index.ContainsKey(key);

    public int IndexOf(string key) => _index.TryGetValue(key, out int i) ? i : -1;

    /// <summary>Ultimo valor de la senal, en las unidades del esquema.</summary>
    public float Get(string key)
    {
        if (!_index.TryGetValue(key, out int i)) return 0f;
        lock (_lock) return (i < _values.Length) ? _values[i] : 0f;
    }

    /// <summary>Valor normalizado 0..1 segun el min/max del esquema.</summary>
    public float GetNormalized(string key)
    {
        if (!SchemaReady || !_index.TryGetValue(key, out int i)) return 0f;
        var d = Schema.sig[i];
        float range = d.max - d.min;
        if (Mathf.Approximately(range, 0f)) return 0f;
        return Mathf.Clamp01((Get(key) - d.min) / range);
    }

    /// <summary>True si la senal se puede fijar desde Unity (w=1 en el esquema).</summary>
    public bool IsWritable(string key) =>
        SchemaReady && _index.TryGetValue(key, out int i) && Schema.sig[i].w == 1;

    /// <summary>Pide a la ESP32 que fije el valor de una senal escribible. El
    /// valor vuelve en el stream, asi que lo que se ve es lo que tiene el micro.</summary>
    public void SetSignal(string key, float value)
    {
        if (!IsWritable(key)) return;
        Send("SET " + key + " " + value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>Copia de todos los canales, en el orden del esquema.</summary>
    public float[] Snapshot()
    {
        lock (_lock)
        {
            float[] copy = new float[_values.Length];
            Array.Copy(_values, copy, _values.Length);
            return copy;
        }
    }
}
