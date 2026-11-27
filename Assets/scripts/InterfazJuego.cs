using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Construye por código toda la interfaz del juego (menú principal, HUD, pausa, terminal de mejoras y victoria)
/// usando los sprites de Assets/Resources/UI, y la conecta al GameManager y al PlayerDrone.
/// GameManager la crea solo (Start) cuando usarInterfazProcedural = true; no hay que arrastrar nada en la escena.
/// Para cambiar el arte basta con reemplazar los PNG de Resources/UI conservando el nombre.
/// </summary>
public class InterfazJuego : MonoBehaviour
{
    // ------------------------------------------------------------------ paleta
    static readonly Color CVerde = new Color(0.29f, 0.87f, 0.50f);
    static readonly Color CVerdeBtn = new Color(0.13f, 0.66f, 0.36f);
    static readonly Color CCian = new Color(0.13f, 0.83f, 0.93f);
    static readonly Color CAmbar = new Color(0.98f, 0.75f, 0.14f);
    static readonly Color CRojo = new Color(1f, 0.42f, 0.42f);
    static readonly Color CRojoBtn = new Color(0.82f, 0.25f, 0.27f);
    static readonly Color CGrisBtn = new Color(0.33f, 0.45f, 0.50f);
    static readonly Color CTexto = new Color(0.93f, 0.98f, 0.97f);
    static readonly Color CSuave = new Color(0.62f, 0.78f, 0.78f);

    static InterfazJuego instancia;

    GameManager gm;
    Canvas canvas;
    readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

    // Paneles
    GameObject menu, menuPortada, menuAyuda, hud, pausa, mejoras, victoria;

    // HUD
    Image mira;
    TextMeshProUGUI txtPlastico, txtMetal, txtVidrio;
    TextMeshProUGUI txtProgresoTitulo, txtProgresoDetalle, txtPorcentaje;
    BarraUI barraProgreso, barraCargaHud;
    TextMeshProUGUI txtCargaValor, txtCargaP, txtCargaM, txtCargaV;
    TextMeshProUGUI txtInteraccion, txtFeedback, txtAyudaHud, txtVictoria;
    CanvasGroup grpInteraccion, grpFeedback;
    readonly Image[] insigniaIcono = new Image[4];
    readonly TextMeshProUGUI[] insigniaNivel = new TextMeshProUGUI[4];

    // Terminal de mejoras
    TextMeshProUGUI txtMatP, txtMatM, txtMatV;
    readonly List<TarjetaUI> tarjetas = new List<TarjetaUI>();

    class BarraUI
    {
        public RectTransform relleno;
        public void Poner(float p)
        {
            p = Mathf.Clamp01(p);
            relleno.gameObject.SetActive(p > 0.001f);
            relleno.anchorMax = new Vector2(p, 1f);
        }
    }

    class TarjetaUI
    {
        public TipoMejora tipo;
        public Image[] pips;
        public TextMeshProUGUI nivel, actual, siguiente, textoBoton;
        public Button boton;
        public GameObject filaCosto;
        public GameObject[] slot = new GameObject[3];
        public TextMeshProUGUI[] cantidad = new TextMeshProUGUI[3];
    }

    // ================================================================== arranque

    public static void Construir(GameManager gestor)
    {
        if (instancia != null) return;
        GameObject go = new GameObject("InterfazJuego");
        instancia = go.AddComponent<InterfazJuego>();
        instancia.Inicializar(gestor);
    }

    void Inicializar(GameManager gestor)
    {
        gm = gestor;

        DesactivarCanvasAntiguo();
        AsegurarEventSystem();
        CrearCanvas();

        ConstruirMenu();
        ConstruirHud();
        ConstruirPausa();
        ConstruirMejoras();
        ConstruirVictoria();

        // Conexión con el GameManager
        gm.panelMenuPrincipal = menu;
        gm.hudGameplay = hud;
        gm.panelPausa = pausa;
        gm.panelMejoras = mejoras;
        gm.panelVictoria = victoria;
        gm.txtInteraccion = txtInteraccion;
        gm.txtFeedback = txtFeedback;

        // Conexión con el dron: el puntero (crosshair) cambia de color según lo que mire
        PlayerDrone dron = FindFirstObjectByType<PlayerDrone>();
        if (dron != null) dron.punteroUI = mira;

        gm.OnEstadoCambiado += Refrescar;
        Refrescar();
    }

    void OnDestroy()
    {
        if (gm != null) gm.OnEstadoCambiado -= Refrescar;
        if (instancia == this) instancia = null;
    }

    void DesactivarCanvasAntiguo()
    {
        GameObject[] viejos = { gm.panelMenuPrincipal, gm.panelPausa, gm.panelMejoras, gm.hudGameplay, gm.panelVictoria };
        foreach (GameObject viejo in viejos)
        {
            if (viejo == null) continue;
            Canvas c = viejo.GetComponentInParent<Canvas>(true);
            if (c != null) c.gameObject.SetActive(false);
        }
    }

    void AsegurarEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;
        GameObject es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        es.transform.SetParent(transform, false);
    }

    void CrearCanvas()
    {
        GameObject go = new GameObject("CanvasInterfaz", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler esc = go.AddComponent<CanvasScaler>();
        esc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        esc.referenceResolution = new Vector2(1920f, 1080f);
        esc.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        esc.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();
    }

    // ================================================================== helpers de UI

    Sprite Spr(string nombre)
    {
        if (sprites.TryGetValue(nombre, out Sprite s)) return s;
        s = Resources.Load<Sprite>("UI/" + nombre);
        if (s == null)
        {
            Debug.LogWarning("InterfazJuego: falta el sprite Resources/UI/" + nombre + ".png");
            s = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        }
        sprites[nombre] = s;
        return s;
    }

    static RectTransform Nuevo(string nombre, Transform padre)
    {
        GameObject g = new GameObject(nombre, typeof(RectTransform));
        RectTransform r = (RectTransform)g.transform;
        r.SetParent(padre, false);
        return r;
    }

    static void Estirar(RectTransform r, float margen = 0f)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(margen, margen);
        r.offsetMax = new Vector2(-margen, -margen);
    }

    /// <summary>Ancla y pivote en el mismo punto; pos es el desplazamiento desde ese punto.</summary>
    static void Fijar(RectTransform r, Vector2 ancla, Vector2 pos, Vector2 tam)
    {
        r.anchorMin = ancla;
        r.anchorMax = ancla;
        r.pivot = ancla;
        r.anchoredPosition = pos;
        r.sizeDelta = tam;
    }

    Image Imagen(Transform padre, string nombre, string sprite, Color color, bool cortado = false, bool bloquea = false)
    {
        RectTransform r = Nuevo(nombre, padre);
        Image im = r.gameObject.AddComponent<Image>();
        im.sprite = Spr(sprite);
        im.color = color;
        im.type = cortado ? Image.Type.Sliced : Image.Type.Simple;
        im.preserveAspect = !cortado;
        im.raycastTarget = bloquea;
        return im;
    }

    /// <summary>Rectángulo de color sólido (sin sprite).</summary>
    Image Solido(Transform padre, string nombre, Color color, bool bloquea)
    {
        RectTransform r = Nuevo(nombre, padre);
        Image im = r.gameObject.AddComponent<Image>();
        im.color = color;
        im.raycastTarget = bloquea;
        return im;
    }

    TextMeshProUGUI Texto(Transform padre, string nombre, string contenido, float tam, Color color,
        TextAlignmentOptions alineacion = TextAlignmentOptions.Center, FontStyles estilo = FontStyles.Normal)
    {
        RectTransform r = Nuevo(nombre, padre);
        TextMeshProUGUI t = r.gameObject.AddComponent<TextMeshProUGUI>();
        t.text = contenido;
        t.fontSize = tam;
        t.color = color;
        t.alignment = alineacion;
        t.fontStyle = estilo;
        t.raycastTarget = false;
        return t;
    }

    Button Boton(Transform padre, string nombre, string texto, Color color, Vector2 tam, UnityAction accion,
        string icono = null, float fuente = 36f)
    {
        RectTransform r = Nuevo(nombre, padre);
        r.sizeDelta = tam;

        Image im = r.gameObject.AddComponent<Image>();
        im.sprite = Spr("ui_boton");
        im.type = Image.Type.Sliced;
        im.color = color;

        Button b = r.gameObject.AddComponent<Button>();
        b.targetGraphic = im;
        ColorBlock cb = b.colors;
        cb.normalColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        cb.highlightedColor = Color.white;
        cb.pressedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
        cb.selectedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
        cb.disabledColor = new Color(0.45f, 0.5f, 0.52f, 0.9f);
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.08f;
        b.colors = cb;
        b.navigation = new Navigation { mode = Navigation.Mode.None };
        if (accion != null) b.onClick.AddListener(accion);

        r.gameObject.AddComponent<EfectoBotonUI>();

        TextMeshProUGUI t = Texto(r, "Texto", texto, fuente, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        Estirar(t.rectTransform);

        if (!string.IsNullOrEmpty(icono))
        {
            float lado = tam.y * 0.56f;
            Image ic = Imagen(r, "Icono", icono, Color.white);
            Fijar(ic.rectTransform, new Vector2(0f, 0.5f), new Vector2(tam.y * 0.32f, 0f), new Vector2(lado, lado));
            t.margin = new Vector4(tam.y * 0.5f, 0f, 0f, 0f);
        }
        return b;
    }

    BarraUI Barra(Transform padre, string nombre, string spriteRelleno, Vector2 ancla, Vector2 pos, Vector2 tam)
    {
        Image fondo = Imagen(padre, nombre, "ui_barra_fondo", Color.white, true);
        Fijar(fondo.rectTransform, ancla, pos, tam);

        RectTransform margen = Nuevo("Margen", fondo.transform);
        Estirar(margen, 5f);

        Image rel = Imagen(margen, "Relleno", spriteRelleno, Color.white, true);
        RectTransform rr = rel.rectTransform;
        rr.anchorMin = Vector2.zero;
        rr.anchorMax = Vector2.one;
        rr.offsetMin = Vector2.zero;
        rr.offsetMax = Vector2.zero;
        return new BarraUI { relleno = rr };
    }

    Image Pildora(Transform padre, string nombre, Vector2 ancla, Vector2 pos, Vector2 tam)
    {
        Image p = Imagen(padre, nombre, "ui_pildora", Color.white, true, true);
        Fijar(p.rectTransform, ancla, pos, tam);
        return p;
    }

    Image PanelCentral(Transform padre, string nombre, Vector2 tam)
    {
        Image p = Imagen(padre, nombre, "ui_panel", Color.white, true, true);
        Fijar(p.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, tam);
        return p;
    }

    static readonly Vector2 TopC = new Vector2(0.5f, 1f);
    static readonly Vector2 BotC = new Vector2(0.5f, 0f);
    static readonly Vector2 MidC = new Vector2(0.5f, 0.5f);
    static readonly Vector2 TopL = new Vector2(0f, 1f);
    static readonly Vector2 TopR = new Vector2(1f, 1f);
    static readonly Vector2 BotL = new Vector2(0f, 0f);
    static readonly Vector2 BotR = new Vector2(1f, 0f);
    static readonly Vector2 MidL = new Vector2(0f, 0.5f);
    static readonly Vector2 MidR = new Vector2(1f, 0.5f);

    // ================================================================== MENÚ PRINCIPAL

    void ConstruirMenu()
    {
        RectTransform raiz = Nuevo("MenuPrincipal", canvas.transform);
        Estirar(raiz);
        menu = raiz.gameObject;

        Image fondo = Imagen(raiz, "Fondo", "ui_fondo_menu", Color.white, false, true);
        fondo.preserveAspect = false;
        Estirar(fondo.rectTransform);

        // ---- portada
        Image panel = PanelCentral(raiz, "Portada", new Vector2(820f, 900f));
        menuPortada = panel.gameObject;

        Image logo = Imagen(panel.transform, "Logo", "logo_titulo", Color.white);
        Fijar(logo.rectTransform, TopC, new Vector2(0f, -48f), new Vector2(210f, 210f));

        TextMeshProUGUI titulo = Texto(panel.transform, "Titulo", "DRON ECO", 92f, CVerde,
            TextAlignmentOptions.Center, FontStyles.Bold);
        Fijar(titulo.rectTransform, TopC, new Vector2(0f, -272f), new Vector2(740f, 110f));

        TextMeshProUGUI sub = Texto(panel.transform, "Subtitulo", "Recicla la basura y restaura el parque", 32f, CTexto);
        Fijar(sub.rectTransform, TopC, new Vector2(0f, -392f), new Vector2(740f, 44f));

        TextMeshProUGUI ods = Texto(panel.transform, "ODS", "ODS 15 · Vida de ecosistemas terrestres", 24f, CSuave);
        Fijar(ods.rectTransform, TopC, new Vector2(0f, -436f), new Vector2(740f, 34f));

        Button jugar = Boton(panel.transform, "BtnJugar", "JUGAR", CVerdeBtn, new Vector2(540f, 100f),
            () => gm.BotonJugar(), "icon_play", 44f);
        Fijar((RectTransform)jugar.transform, TopC, new Vector2(0f, -520f), new Vector2(540f, 100f));

        Button ayuda = Boton(panel.transform, "BtnAyuda", "CÓMO JUGAR", CGrisBtn, new Vector2(540f, 82f),
            () => { menuPortada.SetActive(false); menuAyuda.SetActive(true); }, "icon_dron", 32f);
        Fijar((RectTransform)ayuda.transform, TopC, new Vector2(0f, -640f), new Vector2(540f, 82f));

        Button salir = Boton(panel.transform, "BtnSalir", "SALIR", CRojoBtn, new Vector2(540f, 82f),
            () => gm.BotonSalir(), "icon_cerrar", 32f);
        Fijar((RectTransform)salir.transform, TopC, new Vector2(0f, -742f), new Vector2(540f, 82f));

        TextMeshProUGUI pie = Texto(panel.transform, "Pie", "Esc o P para pausar durante la partida", 22f, CSuave);
        Fijar(pie.rectTransform, BotC, new Vector2(0f, 26f), new Vector2(740f, 30f));

        // ---- ayuda
        Image pAyuda = PanelCentral(raiz, "Ayuda", new Vector2(1080f, 900f));
        menuAyuda = pAyuda.gameObject;

        TextMeshProUGUI tAyuda = Texto(pAyuda.transform, "Titulo", "CÓMO JUGAR", 64f, CVerde,
            TextAlignmentOptions.Center, FontStyles.Bold);
        Fijar(tAyuda.rectTransform, TopC, new Vector2(0f, -36f), new Vector2(900f, 80f));

        TextMeshProUGUI obj = Texto(pAyuda.transform, "Objetivo",
            "Recoge la basura del parque, deposítala en el bote de su color y usa la terminal para mejorar tu dron.",
            27f, CTexto);
        Fijar(obj.rectTransform, TopC, new Vector2(0f, -126f), new Vector2(920f, 80f));

        string[][] controles =
        {
            new[] { "W A S D", "Mover el dron" },
            new[] { "ESPACIO / E", "Subir" },
            new[] { "CTRL / Q", "Bajar" },
            new[] { "MOUSE", "Mirar alrededor" },
            new[] { "CLIC", "Recoger basura (mantén sobre un bote para depositar)" },
            new[] { "SHIFT", "Turbo (se desbloquea con Motores Nv.2)" },
            new[] { "ESC / P", "Pausa" },
        };
        for (int i = 0; i < controles.Length; i++)
        {
            float y = -236f - i * 74f;
            Image tecla = Imagen(pAyuda.transform, "Tecla" + i, "ui_boton", new Color(0.12f, 0.27f, 0.3f), true);
            Fijar(tecla.rectTransform, TopL, new Vector2(70f, y), new Vector2(250f, 56f));
            TextMeshProUGUI tt = Texto(tecla.transform, "Texto", controles[i][0], 26f, CTexto,
                TextAlignmentOptions.Center, FontStyles.Bold);
            Estirar(tt.rectTransform);
            TextMeshProUGUI d = Texto(pAyuda.transform, "Desc" + i, controles[i][1], 26f, CTexto, TextAlignmentOptions.Left);
            Fijar(d.rectTransform, TopL, new Vector2(350f, y), new Vector2(680f, 56f));
        }

        Button volver = Boton(pAyuda.transform, "BtnVolver", "VOLVER", CVerdeBtn, new Vector2(420f, 80f),
            () => { menuAyuda.SetActive(false); menuPortada.SetActive(true); }, null, 34f);
        Fijar((RectTransform)volver.transform, BotC, new Vector2(0f, 34f), new Vector2(420f, 80f));

        menuAyuda.SetActive(false);
    }

    // ================================================================== HUD

    void ConstruirHud()
    {
        RectTransform raiz = Nuevo("HUD", canvas.transform);
        Estirar(raiz);
        hud = raiz.gameObject;

        // ---- mira central (la controla PlayerDrone)
        mira = Imagen(raiz, "Mira", "icon_mira", Color.white);
        Fijar(mira.rectTransform, MidC, Vector2.zero, new Vector2(44f, 44f));

        // ---- materiales (arriba izquierda)
        Image mats = Pildora(raiz, "Materiales", TopL, new Vector2(28f, -28f), new Vector2(540f, 92f));
        txtPlastico = FilaMaterial(mats.transform, "icon_plastico", 22f);
        txtMetal = FilaMaterial(mats.transform, "icon_metal", 192f);
        txtVidrio = FilaMaterial(mats.transform, "icon_vidrio", 362f);

        // ---- progreso del parque (arriba derecha)
        Image prog = Pildora(raiz, "Progreso", TopR, new Vector2(-28f, -28f), new Vector2(620f, 124f));
        Image hoja = Imagen(prog.transform, "Hoja", "icon_hoja", Color.white);
        Fijar(hoja.rectTransform, MidL, new Vector2(24f, 0f), new Vector2(84f, 84f));

        txtProgresoTitulo = Texto(prog.transform, "Titulo", "RESTAURACIÓN DEL PARQUE", 22f, CSuave,
            TextAlignmentOptions.Left, FontStyles.Bold);
        Fijar(txtProgresoTitulo.rectTransform, TopL, new Vector2(124f, -14f), new Vector2(340f, 28f));

        barraProgreso = Barra(prog.transform, "Barra", "ui_barra_relleno", TopL, new Vector2(124f, -48f), new Vector2(340f, 32f));

        txtProgresoDetalle = Texto(prog.transform, "Detalle", "0 / 100 reciclados", 21f, CSuave, TextAlignmentOptions.Left);
        Fijar(txtProgresoDetalle.rectTransform, TopL, new Vector2(124f, -88f), new Vector2(340f, 28f));

        txtPorcentaje = Texto(prog.transform, "Porcentaje", "0%", 50f, CVerde, TextAlignmentOptions.Center, FontStyles.Bold);
        Fijar(txtPorcentaje.rectTransform, MidR, new Vector2(-22f, 0f), new Vector2(140f, 70f));

        // ---- carga del dron (abajo izquierda)
        Image carga = Pildora(raiz, "Carga", BotL, new Vector2(28f, 28f), new Vector2(600f, 156f));
        Image dron = Imagen(carga.transform, "Dron", "icon_dron", Color.white);
        Fijar(dron.rectTransform, MidL, new Vector2(20f, 0f), new Vector2(104f, 104f));

        TextMeshProUGUI tc = Texto(carga.transform, "Titulo", "CARGA", 22f, CSuave, TextAlignmentOptions.Left, FontStyles.Bold);
        Fijar(tc.rectTransform, TopL, new Vector2(144f, -14f), new Vector2(200f, 28f));

        txtCargaValor = Texto(carga.transform, "Valor", "0 / 3", 36f, CTexto, TextAlignmentOptions.Right, FontStyles.Bold);
        Fijar(txtCargaValor.rectTransform, TopR, new Vector2(-24f, -8f), new Vector2(200f, 44f));

        barraCargaHud = Barra(carga.transform, "Barra", "ui_barra_carga", TopL, new Vector2(144f, -58f), new Vector2(432f, 32f));

        txtCargaP = MiniCuenta(carga.transform, "icon_plastico", 144f);
        txtCargaM = MiniCuenta(carga.transform, "icon_metal", 264f);
        txtCargaV = MiniCuenta(carga.transform, "icon_vidrio", 384f);

        // ---- texto de interacción (centro abajo)
        Image pInter = Pildora(raiz, "Interaccion", BotC, new Vector2(0f, 190f), new Vector2(820f, 72f));
        grpInteraccion = pInter.gameObject.AddComponent<CanvasGroup>();
        grpInteraccion.blocksRaycasts = false;
        grpInteraccion.alpha = 0f;
        txtInteraccion = Texto(pInter.transform, "Texto", "", 30f, CTexto, TextAlignmentOptions.Center, FontStyles.Bold);
        Estirar(txtInteraccion.rectTransform, 8f);

        // ---- notificaciones (centro arriba)
        Image pFeed = Pildora(raiz, "Notificacion", TopC, new Vector2(0f, -34f), new Vector2(960f, 72f));
        grpFeedback = pFeed.gameObject.AddComponent<CanvasGroup>();
        grpFeedback.blocksRaycasts = false;
        grpFeedback.alpha = 0f;
        txtFeedback = Texto(pFeed.transform, "Texto", "", 30f, CAmbar, TextAlignmentOptions.Center, FontStyles.Bold);
        Estirar(txtFeedback.rectTransform, 8f);

        // ---- mejoras instaladas (abajo derecha)
        TipoMejora[] tipos = { TipoMejora.Bodega, TipoMejora.Propulsores, TipoMejora.Aspiradora, TipoMejora.Logistica };
        for (int i = 0; i < 4; i++)
        {
            Image circ = Imagen(raiz, "Insignia" + i, "ui_circulo", new Color(0.03f, 0.1f, 0.12f, 0.8f));
            Fijar(circ.rectTransform, BotR, new Vector2(-28f - (3 - i) * 98f, 76f), new Vector2(88f, 88f));
            insigniaIcono[i] = Imagen(circ.transform, "Icono", IconoMejora(tipos[i]), Color.white);
            Fijar(insigniaIcono[i].rectTransform, MidC, new Vector2(0f, 6f), new Vector2(54f, 54f));
            insigniaNivel[i] = Texto(circ.transform, "Nivel", "0/3", 20f, CSuave, TextAlignmentOptions.Center, FontStyles.Bold);
            Fijar(insigniaNivel[i].rectTransform, BotC, new Vector2(0f, 4f), new Vector2(80f, 24f));
        }

        txtAyudaHud = Texto(raiz, "Ayuda", "", 21f, CSuave, TextAlignmentOptions.Right);
        Fijar(txtAyudaHud.rectTransform, BotR, new Vector2(-30f, 30f), new Vector2(1000f, 30f));
    }

    TextMeshProUGUI FilaMaterial(Transform padre, string icono, float x)
    {
        Image ic = Imagen(padre, "Icono", icono, Color.white);
        Fijar(ic.rectTransform, MidL, new Vector2(x, 0f), new Vector2(64f, 64f));
        TextMeshProUGUI t = Texto(padre, "Cantidad", "0", 46f, CTexto, TextAlignmentOptions.Left, FontStyles.Bold);
        Fijar(t.rectTransform, MidL, new Vector2(x + 72f, 0f), new Vector2(90f, 64f));
        return t;
    }

    TextMeshProUGUI MiniCuenta(Transform padre, string icono, float x)
    {
        Image ic = Imagen(padre, "Icono", icono, Color.white);
        Fijar(ic.rectTransform, TopL, new Vector2(x, -104f), new Vector2(36f, 36f));
        TextMeshProUGUI t = Texto(padre, "Cantidad", "0", 26f, CTexto, TextAlignmentOptions.Left, FontStyles.Bold);
        Fijar(t.rectTransform, TopL, new Vector2(x + 42f, -104f), new Vector2(60f, 36f));
        return t;
    }

    // ================================================================== PAUSA

    void ConstruirPausa()
    {
        RectTransform raiz = Nuevo("Pausa", canvas.transform);
        Estirar(raiz);
        pausa = raiz.gameObject;

        Image dim = Solido(raiz, "Oscurecer", new Color(0f, 0.04f, 0.05f, 0.65f), true);
        Estirar(dim.rectTransform);

        Image panel = PanelCentral(raiz, "Panel", new Vector2(640f, 620f));

        TextMeshProUGUI t = Texto(panel.transform, "Titulo", "PAUSA", 72f, CVerde, TextAlignmentOptions.Center, FontStyles.Bold);
        Fijar(t.rectTransform, TopC, new Vector2(0f, -44f), new Vector2(560f, 90f));

        Button reanudar = Boton(panel.transform, "BtnReanudar", "REANUDAR", CVerdeBtn, new Vector2(460f, 88f),
            () => gm.BotonReanudar(), "icon_play", 36f);
        Fijar((RectTransform)reanudar.transform, TopC, new Vector2(0f, -170f), new Vector2(460f, 88f));

        Button menuBtn = Boton(panel.transform, "BtnMenu", "MENÚ PRINCIPAL", CGrisBtn, new Vector2(460f, 82f),
            () => gm.BotonReiniciar(), "icon_hoja", 30f);
        Fijar((RectTransform)menuBtn.transform, TopC, new Vector2(0f, -280f), new Vector2(460f, 82f));

        Button salir = Boton(panel.transform, "BtnSalir", "SALIR", CRojoBtn, new Vector2(460f, 82f),
            () => gm.BotonSalir(), "icon_cerrar", 30f);
        Fijar((RectTransform)salir.transform, TopC, new Vector2(0f, -384f), new Vector2(460f, 82f));

        TextMeshProUGUI pie = Texto(panel.transform, "Pie", "Esc o P para reanudar", 22f, CSuave);
        Fijar(pie.rectTransform, BotC, new Vector2(0f, 28f), new Vector2(560f, 30f));
    }

    // ================================================================== TERMINAL DE MEJORAS

    void ConstruirMejoras()
    {
        RectTransform raiz = Nuevo("Mejoras", canvas.transform);
        Estirar(raiz);
        mejoras = raiz.gameObject;

        Image dim = Solido(raiz, "Oscurecer", new Color(0f, 0.04f, 0.05f, 0.72f), true);
        Estirar(dim.rectTransform);

        Image panel = PanelCentral(raiz, "Panel", new Vector2(1560f, 960f));

        TextMeshProUGUI titulo = Texto(panel.transform, "Titulo", "TERMINAL DE MEJORAS", 56f, CVerde,
            TextAlignmentOptions.Left, FontStyles.Bold);
        Fijar(titulo.rectTransform, TopL, new Vector2(60f, -30f), new Vector2(800f, 70f));

        TextMeshProUGUI sub = Texto(panel.transform, "Subtitulo", "Usa los materiales reciclados para mejorar tu dron",
            26f, CSuave, TextAlignmentOptions.Left);
        Fijar(sub.rectTransform, TopL, new Vector2(62f, -100f), new Vector2(800f, 36f));

        // materiales disponibles
        Image chip = Pildora(panel.transform, "Disponible", TopR, new Vector2(-130f, -28f), new Vector2(640f, 84f));
        TextMeshProUGUI lbl = Texto(chip.transform, "Etiqueta", "DISPONIBLE", 22f, CSuave, TextAlignmentOptions.Left, FontStyles.Bold);
        Fijar(lbl.rectTransform, MidL, new Vector2(24f, 0f), new Vector2(160f, 40f));
        txtMatP = FilaMaterialPeq(chip.transform, "icon_plastico", 196f);
        txtMatM = FilaMaterialPeq(chip.transform, "icon_metal", 340f);
        txtMatV = FilaMaterialPeq(chip.transform, "icon_vidrio", 484f);

        // botón cerrar
        Button cerrar = Boton(panel.transform, "BtnCerrar", "", CRojoBtn, new Vector2(76f, 76f),
            () => gm.BotonCerrarMejoras(), "icon_cerrar", 10f);
        Fijar((RectTransform)cerrar.transform, TopR, new Vector2(-34f, -30f), new Vector2(76f, 76f));
        // el icono ocupa el botón entero
        Image ic = cerrar.transform.Find("Icono").GetComponent<Image>();
        Fijar(ic.rectTransform, MidC, Vector2.zero, new Vector2(44f, 44f));

        // tarjetas
        TipoMejora[] tipos = { TipoMejora.Bodega, TipoMejora.Propulsores, TipoMejora.Aspiradora, TipoMejora.Logistica };
        for (int i = 0; i < tipos.Length; i++)
        {
            tarjetas.Add(ConstruirTarjeta(panel.transform, tipos[i], new Vector2(64f + i * 364f, -158f)));
        }

        TextMeshProUGUI pie = Texto(panel.transform, "Pie", "ESC o TAB para cerrar la terminal", 22f, CSuave);
        Fijar(pie.rectTransform, BotC, new Vector2(0f, 22f), new Vector2(800f, 30f));

        mejoras.SetActive(false);
    }

    TextMeshProUGUI FilaMaterialPeq(Transform padre, string icono, float x)
    {
        Image ic = Imagen(padre, "Icono", icono, Color.white);
        Fijar(ic.rectTransform, MidL, new Vector2(x, 0f), new Vector2(54f, 54f));
        TextMeshProUGUI t = Texto(padre, "Cantidad", "0", 42f, CTexto, TextAlignmentOptions.Left, FontStyles.Bold);
        Fijar(t.rectTransform, MidL, new Vector2(x + 60f, 0f), new Vector2(80f, 54f));
        return t;
    }

    TarjetaUI ConstruirTarjeta(Transform padre, TipoMejora tipo, Vector2 pos)
    {
        TarjetaUI tj = new TarjetaUI { tipo = tipo };
        Color acento = ColorMejora(tipo);

        Image card = Imagen(padre, "Tarjeta_" + tipo, "ui_tarjeta", Color.white, true, true);
        Fijar(card.rectTransform, TopL, pos, new Vector2(340f, 740f));
        Transform c = card.transform;

        Image bg = Imagen(c, "Medallon", "ui_circulo", new Color(acento.r, acento.g, acento.b, 0.22f));
        Fijar(bg.rectTransform, TopC, new Vector2(0f, -26f), new Vector2(160f, 160f));
        Image icono = Imagen(bg.transform, "Icono", IconoMejora(tipo), Color.white);
        Fijar(icono.rectTransform, MidC, Vector2.zero, new Vector2(110f, 110f));

        TextMeshProUGUI titulo = Texto(c, "Nombre", NombreMejora(tipo), 36f, CTexto, TextAlignmentOptions.Center, FontStyles.Bold);
        Fijar(titulo.rectTransform, TopC, new Vector2(0f, -200f), new Vector2(320f, 46f));

        tj.nivel = Texto(c, "Nivel", "NIVEL 0 / 3", 22f, acento, TextAlignmentOptions.Center, FontStyles.Bold);
        Fijar(tj.nivel.rectTransform, TopC, new Vector2(0f, -250f), new Vector2(320f, 30f));

        int max = gm.NivelMaximoDe(tipo);
        tj.pips = new Image[max];
        for (int i = 0; i < max; i++)
        {
            Image pip = Imagen(c, "Pip" + i, "ui_pip_off", Color.white, true);
            Fijar(pip.rectTransform, TopC, new Vector2((i - (max - 1) / 2f) * 84f, -288f), new Vector2(74f, 36f));
            tj.pips[i] = pip;
        }

        tj.actual = Texto(c, "Actual", "", 25f, CSuave, TextAlignmentOptions.Center);
        Fijar(tj.actual.rectTransform, TopC, new Vector2(0f, -340f), new Vector2(310f, 36f));

        TextMeshProUGUI et = Texto(c, "EtSiguiente", "SIGUIENTE NIVEL", 19f, CSuave, TextAlignmentOptions.Center, FontStyles.Bold);
        Fijar(et.rectTransform, TopC, new Vector2(0f, -394f), new Vector2(310f, 26f));

        tj.siguiente = Texto(c, "Siguiente", "", 28f, CAmbar, TextAlignmentOptions.Center, FontStyles.Bold);
        Fijar(tj.siguiente.rectTransform, TopC, new Vector2(0f, -422f), new Vector2(310f, 80f));

        // costos
        RectTransform fila = Nuevo("Costos", c);
        Fijar(fila, TopC, new Vector2(0f, -520f), new Vector2(320f, 100f));
        tj.filaCosto = fila.gameObject;

        TextMeshProUGUI etCosto = Texto(fila, "Etiqueta", "COSTO", 19f, CSuave, TextAlignmentOptions.Center, FontStyles.Bold);
        Fijar(etCosto.rectTransform, TopC, Vector2.zero, new Vector2(320f, 26f));

        string[] iconosCosto = { "icon_plastico", "icon_metal", "icon_vidrio" };
        for (int i = 0; i < 3; i++)
        {
            RectTransform slot = Nuevo("Slot" + i, fila);
            Fijar(slot, TopC, new Vector2(0f, -32f), new Vector2(104f, 60f));
            Image ci = Imagen(slot, "Icono", iconosCosto[i], Color.white);
            Fijar(ci.rectTransform, MidL, Vector2.zero, new Vector2(48f, 48f));
            TextMeshProUGUI ct = Texto(slot, "Cantidad", "0", 36f, CTexto, TextAlignmentOptions.Left, FontStyles.Bold);
            Fijar(ct.rectTransform, MidL, new Vector2(52f, 0f), new Vector2(52f, 56f));
            tj.slot[i] = slot.gameObject;
            tj.cantidad[i] = ct;
        }

        TipoMejora capturado = tipo;
        tj.boton = Boton(c, "BtnMejorar", "MEJORAR", CVerdeBtn, new Vector2(290f, 88f),
            () => gm.Comprar(capturado), null, 34f);
        Fijar((RectTransform)tj.boton.transform, BotC, new Vector2(0f, 26f), new Vector2(290f, 88f));
        tj.textoBoton = tj.boton.GetComponentInChildren<TextMeshProUGUI>();

        return tj;
    }

    // ================================================================== VICTORIA

    void ConstruirVictoria()
    {
        RectTransform raiz = Nuevo("Victoria", canvas.transform);
        Estirar(raiz);
        victoria = raiz.gameObject;

        Image dim = Solido(raiz, "Oscurecer", new Color(0f, 0.05f, 0.04f, 0.75f), true);
        Estirar(dim.rectTransform);

        Image panel = PanelCentral(raiz, "Panel", new Vector2(860f, 760f));

        Image logo = Imagen(panel.transform, "Logo", "logo_titulo", Color.white);
        Fijar(logo.rectTransform, TopC, new Vector2(0f, -40f), new Vector2(190f, 190f));

        TextMeshProUGUI t = Texto(panel.transform, "Titulo", "¡PARQUE RESTAURADO!", 62f, CVerde,
            TextAlignmentOptions.Center, FontStyles.Bold);
        Fijar(t.rectTransform, TopC, new Vector2(0f, -246f), new Vector2(800f, 80f));

        TextMeshProUGUI ods = Texto(panel.transform, "ODS", "Objetivo ODS 15 completado", 32f, CTexto);
        Fijar(ods.rectTransform, TopC, new Vector2(0f, -334f), new Vector2(800f, 44f));

        txtVictoria = Texto(panel.transform, "Estadisticas", "", 28f, CSuave);
        Fijar(txtVictoria.rectTransform, TopC, new Vector2(0f, -386f), new Vector2(800f, 44f));

        Button seguir = Boton(panel.transform, "BtnSeguir", "SEGUIR JUGANDO", CVerdeBtn, new Vector2(500f, 90f),
            () => gm.BotonContinuarTrasVictoria(), "icon_play", 34f);
        Fijar((RectTransform)seguir.transform, TopC, new Vector2(0f, -470f), new Vector2(500f, 90f));

        Button menuBtn = Boton(panel.transform, "BtnMenu", "MENÚ PRINCIPAL", CGrisBtn, new Vector2(500f, 80f),
            () => gm.BotonReiniciar(), "icon_hoja", 30f);
        Fijar((RectTransform)menuBtn.transform, TopC, new Vector2(0f, -578f), new Vector2(500f, 80f));
    }

    // ================================================================== actualización

    static string IconoMejora(TipoMejora t)
    {
        switch (t)
        {
            case TipoMejora.Bodega: return "icon_bodega";
            case TipoMejora.Propulsores: return "icon_motor";
            case TipoMejora.Aspiradora: return "icon_iman";
            default: return "icon_logistica";
        }
    }

    static string NombreMejora(TipoMejora t)
    {
        switch (t)
        {
            case TipoMejora.Bodega: return "BODEGA";
            case TipoMejora.Propulsores: return "MOTORES";
            case TipoMejora.Aspiradora: return "IMÁN";
            default: return "LOGÍSTICA";
        }
    }

    static Color ColorMejora(TipoMejora t)
    {
        switch (t)
        {
            case TipoMejora.Bodega: return CAmbar;
            case TipoMejora.Propulsores: return CCian;
            case TipoMejora.Aspiradora: return CRojo;
            default: return CVerde;
        }
    }

    string TextoActual(TipoMejora t)
    {
        switch (t)
        {
            case TipoMejora.Bodega: return "Capacidad: " + gm.capacidadDron;
            case TipoMejora.Propulsores: return "Velocidad: " + gm.velocidadDron.ToString("0.#") + " m/s";
            case TipoMejora.Aspiradora:
                return gm.EsRecoleccionAutomatica ? "Alcance: " + gm.radioAspiracion.ToString("0.#") + " m" : "Recogida manual (clic)";
            default: return "Descarga cada " + gm.VelocidadDescargaActual.ToString("0.##") + " s";
        }
    }

    void Refrescar()
    {
        if (gm == null) return;

        // HUD: materiales
        if (txtPlastico != null) txtPlastico.text = gm.plastico.ToString();
        if (txtMetal != null) txtMetal.text = gm.metal.ToString();
        if (txtVidrio != null) txtVidrio.text = gm.vidrio.ToString();

        // HUD: progreso
        float p = gm.PorcentajeRestauracion;
        if (barraProgreso != null) barraProgreso.Poner(p);
        if (txtPorcentaje != null) txtPorcentaje.text = Mathf.RoundToInt(p * 100f) + "%";
        if (txtProgresoDetalle != null) txtProgresoDetalle.text = gm.totalReciclado + " / " + gm.metaReciclajeTotal + " reciclados";

        // HUD: carga
        float cap = Mathf.Max(1, gm.capacidadDron);
        if (barraCargaHud != null) barraCargaHud.Poner(gm.CargaActual / cap);
        if (txtCargaValor != null)
        {
            txtCargaValor.text = gm.CargaActual + " / " + gm.capacidadDron;
            txtCargaValor.color = gm.CargaActual >= gm.capacidadDron ? CRojo : CTexto;
        }
        if (txtCargaP != null) txtCargaP.text = gm.CargaP.ToString();
        if (txtCargaM != null) txtCargaM.text = gm.CargaM.ToString();
        if (txtCargaV != null) txtCargaV.text = gm.CargaV.ToString();

        // HUD: insignias de mejoras instaladas
        TipoMejora[] tipos = { TipoMejora.Bodega, TipoMejora.Propulsores, TipoMejora.Aspiradora, TipoMejora.Logistica };
        for (int i = 0; i < 4; i++)
        {
            if (insigniaIcono[i] == null) continue;
            int nivel = gm.NivelDe(tipos[i]);
            int max = gm.NivelMaximoDe(tipos[i]);
            insigniaIcono[i].color = nivel > 0 ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            insigniaNivel[i].text = nivel + "/" + max;
            insigniaNivel[i].color = nivel >= max ? CAmbar : (nivel > 0 ? CVerde : CSuave);
        }

        if (txtAyudaHud != null)
        {
            txtAyudaHud.text = "WASD mover  |  Espacio / Ctrl subir y bajar  |  Clic recoger  |  Esc pausa" +
                               (gm.TieneTurbo ? "  |  Shift turbo" : "");
        }

        if (txtVictoria != null) txtVictoria.text = "Reciclaste " + gm.totalReciclado + " objetos para salvar el parque";

        // Terminal de mejoras
        if (txtMatP != null) txtMatP.text = gm.plastico.ToString();
        if (txtMatM != null) txtMatM.text = gm.metal.ToString();
        if (txtMatV != null) txtMatV.text = gm.vidrio.ToString();

        foreach (TarjetaUI tj in tarjetas) RefrescarTarjeta(tj);
    }

    void RefrescarTarjeta(TarjetaUI tj)
    {
        int nivel = gm.NivelDe(tj.tipo);
        int max = gm.NivelMaximoDe(tj.tipo);

        for (int i = 0; i < tj.pips.Length; i++)
            tj.pips[i].sprite = Spr(i < nivel ? "ui_pip_on" : "ui_pip_off");

        tj.nivel.text = "NIVEL " + nivel + " / " + max;
        tj.actual.text = TextoActual(tj.tipo);

        if (gm.TryGetSiguienteNivel(tj.tipo, out GameManager.NivelMejora sig))
        {
            tj.siguiente.text = sig.descripcion;
            tj.siguiente.color = CAmbar;
            tj.filaCosto.SetActive(true);

            int[] costos = { sig.costoP, sig.costoM, sig.costoV };
            int[] tengo = { gm.plastico, gm.metal, gm.vidrio };

            int activos = 0;
            for (int i = 0; i < 3; i++) if (costos[i] > 0) activos++;

            int k = 0;
            for (int i = 0; i < 3; i++)
            {
                bool usa = costos[i] > 0;
                tj.slot[i].SetActive(usa);
                if (!usa) continue;

                RectTransform rs = (RectTransform)tj.slot[i].transform;
                rs.anchoredPosition = new Vector2((k - (activos - 1) / 2f) * 108f, -32f);
                k++;

                tj.cantidad[i].text = "x" + costos[i];
                tj.cantidad[i].color = tengo[i] >= costos[i] ? CTexto : CRojo;
            }

            tj.boton.interactable = gm.PuedeComprar(tj.tipo);
            tj.textoBoton.text = "MEJORAR";
        }
        else
        {
            tj.siguiente.text = "¡Mejora completada!";
            tj.siguiente.color = CVerde;
            tj.filaCosto.SetActive(false);
            tj.boton.interactable = false;
            tj.textoBoton.text = "NIVEL MÁXIMO";
        }
    }

    void Update()
    {
        if (gm == null) return;
        Desvanecer(grpInteraccion, txtInteraccion);
        Desvanecer(grpFeedback, txtFeedback);
    }

    static void Desvanecer(CanvasGroup grupo, TextMeshProUGUI texto)
    {
        if (grupo == null || texto == null) return;
        float objetivo = string.IsNullOrEmpty(texto.text) ? 0f : 1f;
        grupo.alpha = Mathf.MoveTowards(grupo.alpha, objetivo, 8f * Time.unscaledDeltaTime);
    }
}
