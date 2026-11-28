using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Si Unity te avisa que 'TipoBasura' no existe (porque antes estaba declarado aquí),
// simplemente borra las dos barras "//" de la línea de abajo:
// public enum TipoBasura { Plastico, Metal, Vidrio }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

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

    // Estados internos
    private bool enMenuPrincipal = true;
    private bool enPausa = false;
    private bool enMejoras = false;
    private bool victoriaAlcanzada = false;

    // Estado de carga actual del dron (para refrescar HUD al mejorar bodega)
    private int cargaTotalActual = 0;
    private int cargaP = 0, cargaM = 0, cargaV = 0;
    private Coroutine corrutinaFeedback;

    // Propiedades que lee PlayerDrone.cs
    public bool EstaJugando => !enMenuPrincipal && !enPausa && !enMejoras;
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
        else Destroy(gameObject);
    }

    void Start()
    {
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
        if (Keyboard.current == null || enMenuPrincipal) return;

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
            if (panelVictoria != null) panelVictoria.SetActive(true);
        }
    }

    public void ActualizarUI()
    {
        if (txtPuntosInventario != null)
        {
            txtPuntosInventario.text = $"Plástico: {plastico}  |  Metal: {metal}  |  Vidrio: {vidrio}";
        }

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