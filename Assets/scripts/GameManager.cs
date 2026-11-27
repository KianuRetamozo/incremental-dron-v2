using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Si Unity te avisa que 'TipoBasura' no existe (porque antes estaba declarado aquí),
// simplemente borra las dos barras "//" de la línea de abajo:
// public enum TipoBasura { Plastico, Metal, Vidrio }

public enum TipoMejora { Bodega, Propulsores, Aspiradora, Logistica }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    /// <summary>Se dispara cada vez que cambia algo que la interfaz debe redibujar.</summary>
    public event System.Action OnEstadoCambiado;

    [System.Serializable]
    public struct NivelMejora
    {
        public string nombreCorto;
        public string descripcion;
        public float valorPrincipal;
        public int costoP;
        public int costoM;
        public int costoV;
    }

    [Header("1. Estadísticas Base del Dron")]
    public int capacidadDron = 3;
    public float velocidadDron = 7f;
    public float radioAspiracion = 1.8f;

    [Header("2. Recursos Reciclados y Meta ODS 15")]
    public int plastico = 0;
    public int metal = 0;
    public int vidrio = 0;
    public int totalReciclado = 0;
    public int metaReciclajeTotal = 100;

    [Header("3. Fases de Restauración del Bosque")]
    public GameObject[] fasesBosque;
    public int[] hitosFases = new int[] { 15, 45, 100 };

    [Header("4. Niveles de Mejoras (0 a 3)")]
    public int nivelBodega = 0;
    public int nivelPropulsores = 0;
    public int nivelAspiradora = 0;
    public int nivelLogistica = 0;

    [Header("5. Paneles de Interfaz")]
    public GameObject panelMenuPrincipal;
    public GameObject panelPausa;
    public GameObject panelMejoras;
    public GameObject hudGameplay;
    public GameObject panelVictoria; // Opcional

    [Header("6. Textos y Barras del HUD")]
    public TextMeshProUGUI txtPuntosInventario;
    public TextMeshProUGUI txtCarga;
    public TextMeshProUGUI txtInteraccion;
    public TextMeshProUGUI txtFeedback;
    public TextMeshProUGUI txtProgresoParque;
    public Slider barraProgresoParque; // Opcional si usas Slider

    [Header("7. Botones del Panel de Mejoras")]
    public Button btnMejoraBodega;
    public TextMeshProUGUI txtMejoraBodega;
    public Button btnMejoraPropulsores;
    public TextMeshProUGUI txtMejoraPropulsores;
    public Button btnMejoraAspiradora;
    public TextMeshProUGUI txtMejoraAspiradora;
    public Button btnMejoraLogistica;
    public TextMeshProUGUI txtMejoraLogistica;
    public TextMeshProUGUI txtRecursosEnTerminal; // Opcional

    [Header("8. Interfaz Procedural (InterfazJuego.cs)")]
    [Tooltip("Si está activo, la interfaz (menú, HUD, pausa, mejoras, victoria) se construye por código " +
             "con los sprites de Resources/UI y reemplaza el Canvas antiguo de la escena.")]
    public bool usarInterfazProcedural = true;
    public TextMeshProUGUI txtPlastico;   // Contadores individuales del HUD (opcionales)
    public TextMeshProUGUI txtMetal;
    public TextMeshProUGUI txtVidrio;
    public Image barraCarga;              // Relleno (Filled) de la carga del dron

    // Estados internos
    private bool enMenuPrincipal = true;
    private bool enPausa = false;
    private bool enMejoras = false;
    private bool victoriaAlcanzada = false;
    private bool enVictoria = false;

    // Lectura de estado para la interfaz
    public bool EnMenuPrincipal => enMenuPrincipal;
    public bool EnPausa => enPausa;
    public bool EnMejoras => enMejoras;
    public int CargaActual => cargaTotalActual;
    public int CargaP => cargaP;
    public int CargaM => cargaM;
    public int CargaV => cargaV;
    public float PorcentajeRestauracion => metaReciclajeTotal > 0
        ? Mathf.Clamp01((float)totalReciclado / metaReciclajeTotal) : 0f;

    // Estado de carga actual del dron (para refrescar HUD al mejorar bodega)
    private int cargaTotalActual = 0;
    private int cargaP = 0, cargaM = 0, cargaV = 0;
    private Coroutine corrutinaFeedback;

    // Propiedades que lee PlayerDrone.cs
    public bool EstaJugando => !enMenuPrincipal && !enPausa && !enMejoras && !enVictoria;
    public bool EsRecoleccionAutomatica => nivelAspiradora >= 1;
    public bool TieneTurbo => nivelPropulsores >= 2;
    public float MultiplicadorTurbo => nivelPropulsores >= 3 ? 1.45f : 1.25f;
    public float VelocidadDescargaActual => nivelLogistica >= 1 ? 0.05f : 0.2f;
    public bool ClasificadorUniversal => nivelLogistica >= 2;
    public bool DescargaAutomaticaEstacion => nivelLogistica >= 3;

    // Tablas de progresión de mejoras
    private readonly NivelMejora[] tablaBodega = {
        new NivelMejora { nombreCorto = "Bodega Nv.1", descripcion = "Capacidad: 6",  valorPrincipal = 6,  costoP = 2, costoM = 1, costoV = 0 },
        new NivelMejora { nombreCorto = "Bodega Nv.2", descripcion = "Capacidad: 12", valorPrincipal = 12, costoP = 4, costoM = 2, costoV = 0 },
        new NivelMejora { nombreCorto = "Bodega Nv.3", descripcion = "Capacidad: 20", valorPrincipal = 20, costoP = 6, costoM = 3, costoV = 0 }
    };

    private readonly NivelMejora[] tablaPropulsores = {
        new NivelMejora { nombreCorto = "Motores Nv.1", descripcion = "Vel: 10.5 m/s",            valorPrincipal = 10.5f, costoP = 0, costoM = 2, costoV = 1 },
        new NivelMejora { nombreCorto = "Motores Nv.2", descripcion = "Vel: 14 + Turbo [Shift]",  valorPrincipal = 14f,   costoP = 0, costoM = 4, costoV = 2 },
        new NivelMejora { nombreCorto = "Motores Nv.3", descripcion = "Vel: 17 + Super Turbo",    valorPrincipal = 17f,   costoP = 0, costoM = 6, costoV = 3 }
    };

    private readonly NivelMejora[] tablaAspiradora = {
        new NivelMejora { nombreCorto = "Imán Nv.1", descripcion = "Auto-recogida (2.8m)", valorPrincipal = 2.8f, costoP = 1, costoM = 0, costoV = 2 },
        new NivelMejora { nombreCorto = "Imán Nv.2", descripcion = "Imán medio (5m)",      valorPrincipal = 5.0f, costoP = 2, costoM = 0, costoV = 4 },
        new NivelMejora { nombreCorto = "Imán Nv.3", descripcion = "Vórtice amplio (8m)",  valorPrincipal = 8.0f, costoP = 3, costoM = 0, costoV = 6 }
    };

    private readonly NivelMejora[] tablaLogistica = {
        new NivelMejora { nombreCorto = "Logística Nv.1", descripcion = "Descarga 4x rápida",     valorPrincipal = 1, costoP = 1, costoM = 1, costoV = 1 },
        new NivelMejora { nombreCorto = "Logística Nv.2", descripcion = "Botes universales",      valorPrincipal = 2, costoP = 2, costoM = 2, costoV = 2 },
        new NivelMejora { nombreCorto = "Logística Nv.3", descripcion = "Descarga auto al pasar", valorPrincipal = 3, costoP = 4, costoM = 4, costoV = 4 }
    };

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // La escena guardaba valores de una versión anterior (vel. 4, radio 1.5). Las tablas de mejoras
        // están calculadas sobre estas estadísticas base, así que se normalizan al iniciar.
        capacidadDron = 3;
        velocidadDron = 7f;
        radioAspiracion = 1.8f;
        nivelBodega = nivelPropulsores = nivelAspiradora = nivelLogistica = 0;
        Time.timeScale = 1f;
    }

    void Start()
    {
        // Reemplaza el Canvas antiguo por la interfaz con sprites y la conecta a este GameManager
        if (usarInterfazProcedural) InterfazJuego.Construir(this);

        // Oculta las fases del bosque al iniciar
        if (fasesBosque != null)
        {
            foreach (GameObject fase in fasesBosque)
            {
                if (fase != null) fase.SetActive(false);
            }
        }

        if (panelPausa != null) panelPausa.SetActive(false);
        if (panelMejoras != null) panelMejoras.SetActive(false);
        if (panelVictoria != null) panelVictoria.SetActive(false);
        if (txtFeedback != null) txtFeedback.text = "";
        if (txtInteraccion != null) txtInteraccion.text = "";

        if (panelMenuPrincipal != null)
        {
            enMenuPrincipal = true;
            panelMenuPrincipal.SetActive(true);
            if (hudGameplay != null) hudGameplay.SetActive(false);
            ConfigurarCursorYPausa(true);
        }
        else
        {
            enMenuPrincipal = false;
            if (hudGameplay != null) hudGameplay.SetActive(true);
            ConfigurarCursorYPausa(false);
        }

        ActualizarUI();
        ActualizarPanelMejoras();
    }

    void Update()
    {
        if (Keyboard.current == null || enMenuPrincipal || enVictoria) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (enMejoras) BotonCerrarMejoras();
            else if (enPausa) BotonReanudar();
            else PausarJuego();
        }
        else if (Keyboard.current.pKey.wasPressedThisFrame && !enMejoras)
        {
            if (enPausa) BotonReanudar();
            else PausarJuego();
        }
        else if (Keyboard.current.tabKey.wasPressedThisFrame && enMejoras)
        {
            BotonCerrarMejoras();
        }
    }

    // ==================== RECICLAJE Y PROGRESO ====================

    public void DepositarUnMaterial(TipoBasura tipo, int cargaRestante, int p, int m, int v)
    {
        if (tipo == TipoBasura.Plastico) plastico++;
        else if (tipo == TipoBasura.Metal) metal++;
        else if (tipo == TipoBasura.Vidrio) vidrio++;

        totalReciclado++;
        ActualizarCargaHUD(cargaRestante, p, m, v);
        ActualizarFasesBosque();
        ActualizarUI();
        ActualizarPanelMejoras();
    }

    public void ActualizarCargaHUD(int cargaTotal, int p, int m, int v)
    {
        cargaTotalActual = cargaTotal;
        cargaP = p;
        cargaM = m;
        cargaV = v;

        if (txtCarga != null)
        {
            txtCarga.text = $"Carga: {cargaTotalActual}/{capacidadDron}  (P:{cargaP} M:{cargaM} V:{cargaV})";
        }

        if (barraCarga != null)
        {
            barraCarga.fillAmount = capacidadDron > 0 ? Mathf.Clamp01((float)cargaTotalActual / capacidadDron) : 0f;
        }

        OnEstadoCambiado?.Invoke();
    }

    void ActualizarFasesBosque()
    {
        if (fasesBosque == null || hitosFases == null) return;

        int limite = Mathf.Min(fasesBosque.Length, hitosFases.Length);
        for (int i = 0; i < limite; i++)
        {
            if (fasesBosque[i] != null && !fasesBosque[i].activeSelf && totalReciclado >= hitosFases[i])
            {
                fasesBosque[i].SetActive(true);
                MostrarFeedback($"¡Ecosistema restaurado: Fase {i + 1} desbloqueada!", 3f);
            }
        }

        if (!victoriaAlcanzada && totalReciclado >= metaReciclajeTotal)
        {
            victoriaAlcanzada = true;
            MostrarFeedback("¡Parque 100% restaurado! Objetivo ODS 15 completado", 4f);
            if (panelVictoria != null)
            {
                enVictoria = true;
                panelVictoria.SetActive(true);
                ConfigurarCursorYPausa(true);
            }
        }
    }

    /// <summary>Cierra el panel de victoria y sigue jugando (el parque ya está restaurado).</summary>
    public void BotonContinuarTrasVictoria()
    {
        enVictoria = false;
        if (panelVictoria != null) panelVictoria.SetActive(false);
        ConfigurarCursorYPausa(false);
    }

    public void ActualizarUI()
    {
        if (txtPuntosInventario != null)
        {
            txtPuntosInventario.text = $"Plástico: {plastico}  |  Metal: {metal}  |  Vidrio: {vidrio}";
        }

        if (txtPlastico != null) txtPlastico.text = plastico.ToString();
        if (txtMetal != null) txtMetal.text = metal.ToString();
        if (txtVidrio != null) txtVidrio.text = vidrio.ToString();

        ActualizarCargaHUD(cargaTotalActual, cargaP, cargaM, cargaV);

        float porcentaje = metaReciclajeTotal > 0
            ? Mathf.Clamp01((float)totalReciclado / metaReciclajeTotal)
            : 0f;

        if (barraProgresoParque != null)
        {
            barraProgresoParque.value = porcentaje;
        }

        if (txtProgresoParque != null)
        {
            txtProgresoParque.text = $"Restauración del Parque: {Mathf.RoundToInt(porcentaje * 100f)}% ({totalReciclado}/{metaReciclajeTotal})";
        }
    }

    // ==================== SISTEMA DE MEJORAS ====================

    public void ComprarMejoraBodega()
    {
        if (IntentarComprar(tablaBodega, ref nivelBodega, out NivelMejora m))
        {
            capacidadDron = Mathf.RoundToInt(m.valorPrincipal);
            FinalizarCompra($"{m.nombreCorto} instalada");
        }
    }

    public void ComprarMejoraPropulsores()
    {
        if (IntentarComprar(tablaPropulsores, ref nivelPropulsores, out NivelMejora m))
        {
            velocidadDron = m.valorPrincipal;
            FinalizarCompra($"{m.nombreCorto} instalada");
        }
    }

    public void ComprarMejoraAspiradora()
    {
        if (IntentarComprar(tablaAspiradora, ref nivelAspiradora, out NivelMejora m))
        {
            radioAspiracion = m.valorPrincipal;
            FinalizarCompra($"{m.nombreCorto} instalada");
        }
    }

    public void ComprarMejoraLogistica()
    {
        if (IntentarComprar(tablaLogistica, ref nivelLogistica, out NivelMejora m))
        {
            FinalizarCompra($"{m.nombreCorto} instalada");
        }
    }

    // ---- API genérica por tipo (la usa la interfaz con sprites) ----

    NivelMejora[] TablaDe(TipoMejora t)
    {
        switch (t)
        {
            case TipoMejora.Bodega: return tablaBodega;
            case TipoMejora.Propulsores: return tablaPropulsores;
            case TipoMejora.Aspiradora: return tablaAspiradora;
            default: return tablaLogistica;
        }
    }

    public int NivelDe(TipoMejora t)
    {
        switch (t)
        {
            case TipoMejora.Bodega: return nivelBodega;
            case TipoMejora.Propulsores: return nivelPropulsores;
            case TipoMejora.Aspiradora: return nivelAspiradora;
            default: return nivelLogistica;
        }
    }

    public int NivelMaximoDe(TipoMejora t) => TablaDe(t).Length;

    /// <summary>Datos del siguiente nivel. Devuelve false si la mejora ya está al máximo.</summary>
    public bool TryGetSiguienteNivel(TipoMejora t, out NivelMejora siguiente)
    {
        NivelMejora[] tabla = TablaDe(t);
        int nivel = NivelDe(t);
        if (nivel >= tabla.Length) { siguiente = default; return false; }
        siguiente = tabla[nivel];
        return true;
    }

    public bool PuedeComprar(TipoMejora t)
    {
        return TryGetSiguienteNivel(t, out NivelMejora s) && TieneRecursos(s);
    }

    public void Comprar(TipoMejora t)
    {
        switch (t)
        {
            case TipoMejora.Bodega: ComprarMejoraBodega(); break;
            case TipoMejora.Propulsores: ComprarMejoraPropulsores(); break;
            case TipoMejora.Aspiradora: ComprarMejoraAspiradora(); break;
            default: ComprarMejoraLogistica(); break;
        }
    }

    bool IntentarComprar(NivelMejora[] tabla, ref int nivelActual, out NivelMejora mejora)
    {
        mejora = default;
        if (nivelActual >= tabla.Length) return false;

        NivelMejora siguiente = tabla[nivelActual];
        if (!TieneRecursos(siguiente))
        {
            MostrarFeedback("Materiales insuficientes", 1.2f);
            return false;
        }

        plastico -= siguiente.costoP;
        metal -= siguiente.costoM;
        vidrio -= siguiente.costoV;
        nivelActual++;
        mejora = siguiente;
        return true;
    }

    bool TieneRecursos(NivelMejora m)
    {
        return plastico >= m.costoP && metal >= m.costoM && vidrio >= m.costoV;
    }

    void FinalizarCompra(string mensaje)
    {
        ActualizarUI();
        ActualizarPanelMejoras();
        MostrarFeedback(mensaje, 1.8f);
    }

    public void ActualizarPanelMejoras()
    {
        RefrescarBoton(btnMejoraBodega, txtMejoraBodega, tablaBodega, nivelBodega, "BODEGA AL MÁXIMO");
        RefrescarBoton(btnMejoraPropulsores, txtMejoraPropulsores, tablaPropulsores, nivelPropulsores, "MOTORES AL MÁXIMO");
        RefrescarBoton(btnMejoraAspiradora, txtMejoraAspiradora, tablaAspiradora, nivelAspiradora, "IMÁN AL MÁXIMO");
        RefrescarBoton(btnMejoraLogistica, txtMejoraLogistica, tablaLogistica, nivelLogistica, "LOGÍSTICA AL MÁXIMO");

        if (txtRecursosEnTerminal != null)
        {
            txtRecursosEnTerminal.text = $"Disponible — Plástico: {plastico} | Metal: {metal} | Vidrio: {vidrio}";
        }

        OnEstadoCambiado?.Invoke();
    }

    void RefrescarBoton(Button btn, TextMeshProUGUI txt, NivelMejora[] tabla, int nivelActual, string textoMax)
    {
        if (nivelActual >= tabla.Length)
        {
            if (txt != null) txt.text = textoMax;
            if (btn != null) btn.interactable = false;
            return;
        }

        NivelMejora m = tabla[nivelActual];
        bool puedeComprar = TieneRecursos(m);

        string costos = "";
        if (m.costoP > 0) costos += $"P:{m.costoP} ";
        if (m.costoM > 0) costos += $"M:{m.costoM} ";
        if (m.costoV > 0) costos += $"V:{m.costoV}";

        if (txt != null) txt.text = $"{m.nombreCorto} — {m.descripcion}\nCosto: {costos.Trim()}";
        if (btn != null) btn.interactable = puedeComprar;
    }

    // ==================== MENÚS, PAUSA Y FEEDBACK ====================

    public void BotonJugar()
    {
        enMenuPrincipal = false;
        enPausa = false;
        enMejoras = false;

        if (panelMenuPrincipal != null) panelMenuPrincipal.SetActive(false);
        if (panelPausa != null) panelPausa.SetActive(false);
        if (panelMejoras != null) panelMejoras.SetActive(false);
        if (hudGameplay != null) hudGameplay.SetActive(true);

        ConfigurarCursorYPausa(false);
        ActualizarUI();
    }

    public void PausarJuego()
    {
        enPausa = true;
        if (panelPausa != null) panelPausa.SetActive(true);
        ConfigurarCursorYPausa(true);
    }

    public void BotonReanudar()
    {
        enPausa = false;
        if (panelPausa != null) panelPausa.SetActive(false);
        ConfigurarCursorYPausa(false);
    }

    public void BotonAbrirMejoras()
    {
        enMejoras = true;
        if (panelMejoras != null) panelMejoras.SetActive(true);
        ActualizarPanelMejoras();
        ConfigurarCursorYPausa(true);
    }

    public void BotonCerrarMejoras()
    {
        enMejoras = false;
        if (panelMejoras != null) panelMejoras.SetActive(false);
        ConfigurarCursorYPausa(false);
    }

    public void BotonReiniciar()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void BotonSalir()
    {
        Application.Quit();
        Debug.Log("Salir del juego ejecutado.");
    }

    void ConfigurarCursorYPausa(bool pausar)
    {
        Time.timeScale = pausar ? 0f : 1f;
        Cursor.lockState = pausar ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = pausar;
    }

    public void MostrarInteraccion(string texto)
    {
        if (txtInteraccion != null) txtInteraccion.text = texto;
    }

    public void MostrarFeedback(string mensaje, float duracion = 1.5f)
    {
        if (txtFeedback == null) return;
        if (corrutinaFeedback != null) StopCoroutine(corrutinaFeedback);
        corrutinaFeedback = StartCoroutine(RutinaFeedback(mensaje, duracion));
    }

    IEnumerator RutinaFeedback(string mensaje, float duracion)
    {
        txtFeedback.text = mensaje;
        yield return new WaitForSecondsRealtime(duracion);
        txtFeedback.text = "";
    }
}