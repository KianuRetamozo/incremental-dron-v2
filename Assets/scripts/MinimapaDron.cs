using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Minimapa para el dron. Se crea SOLO al iniciar la escena (no hay que configurar nada).
/// - Cámara cenital -> RenderTexture -> RawImage en una esquina.
/// - Flecha cian = el dron (hacia donde mira). Puntos de color = basura. Cuadrados = contenedores.
///   Rombo amarillo = terminal de mejoras. Anillo blanco = la basura más cercana
///   (si está fuera del minimapa, el anillo se pega al borde indicando la dirección).
/// - Tecla M = mapa grande de todo el terreno (norte arriba). Vuelve a pulsar M para cerrar.
/// Si quieres cambiar valores (esquina, zoom, tamaño), pon este script en un GameObject vacío
/// de la escena y edítalo en el Inspector: así no se crea uno automático.
/// </summary>
public class MinimapaDron : MonoBehaviour
{
    public enum Esquina { SuperiorDerecha, SuperiorIzquierda, InferiorDerecha, InferiorIzquierda }

    [Header("Minimapa (esquina de la pantalla)")]
    public Esquina esquina = Esquina.SuperiorDerecha;
    public float tamanoPequeno = 280f;
    public float margenPantalla = 30f;
    [Tooltip("Metros visibles desde el centro hacia cada lado (menor = más zoom).")]
    public float zoomPequeno = 22f;
    [Tooltip("Si está activo, 'arriba' del minimapa es hacia donde mira el dron.")]
    public bool rotarConDron = true;

    [Header("Mapa grande (tecla M)")]
    public Key teclaMapaGrande = Key.M;
    public float tamanoGrande = 760f;
    public Vector2 centroMundo = Vector2.zero;   // El dron se limita a +-limiteMapa alrededor de (0,0)

    [Header("Cámara cenital")]
    public float alturaCamara = 150f;
    public int resolucionTextura = 1024;
    public Color colorFondo = new Color(0.08f, 0.13f, 0.10f);
    [Tooltip("Capas que dibuja el minimapa. Quita aquí capas que se vean mal desde arriba (nubes, lluvia, etc.).")]
    public LayerMask capasVisibles = ~0;

    // ---------- Colores ----------
    static readonly Color cPlastico = new Color(0.30f, 0.70f, 1.00f);
    static readonly Color cMetal = new Color(1.00f, 0.60f, 0.20f);
    static readonly Color cVidrio = new Color(0.90f, 0.40f, 1.00f);
    static readonly Color cTerminal = new Color(1.00f, 0.90f, 0.20f);
    static readonly Color cDron = new Color(0.15f, 0.95f, 0.85f);

    const float BORDE = 4f;
    const float ALTO_BARRA = 26f;

    struct Basura { public Transform t; public TipoBasura tipo; }
    struct Contenedor { public Transform t; public Color color; }

    PlayerDrone drone;
    Camera cam;
    RenderTexture rt;
    Canvas canvas;
    RectTransform root, iconos;
    TextMeshProUGUI etiqueta;
    Image flecha, anillo;
    Sprite sprCirculo, sprAnillo, sprFlecha;

    readonly List<Basura> basuras = new List<Basura>();
    readonly List<Contenedor> contenedores = new List<Contenedor>();
    readonly List<Transform> terminales = new List<Transform>();
    readonly List<Image> iconosBasura = new List<Image>();
    readonly List<Image> iconosContenedor = new List<Image>();
    readonly List<Image> iconosTerminal = new List<Image>();

    bool mapaGrande;
    float tamanoActual;
    float proximoRefresco;
    float proximaEtiqueta;
    float camYaw;

    // ==================== Arranque automático ====================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
        SceneManager.sceneLoaded -= AlCargarEscena;
        SceneManager.sceneLoaded += AlCargarEscena;
        CrearSiHaceFalta();
    }

    static void AlCargarEscena(Scene escena, LoadSceneMode modo)
    {
        CrearSiHaceFalta();
    }

    static void CrearSiHaceFalta()
    {
        if (FindFirstObjectByType<PlayerDrone>() == null) return;
        if (FindFirstObjectByType<MinimapaDron>() != null) return;
        new GameObject("MinimapaDron").AddComponent<MinimapaDron>();
    }

    // ==================== Construcción ====================

    void Awake()
    {
        sprCirculo = CrearSpriteCirculo(64, false);
        sprAnillo = CrearSpriteCirculo(64, true);
        sprFlecha = CrearSpriteFlecha(64);

        CrearCamara();
        CrearInterfaz();
        AplicarModo();
    }

    void CrearCamara()
    {
        rt = new RenderTexture(resolucionTextura, resolucionTextura, 16, RenderTextureFormat.ARGB32);
        rt.name = "MinimapaRT";
        rt.Create();

        GameObject go = new GameObject("CamaraMinimapa");
        go.transform.SetParent(transform, false);

        cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = zoomPequeno;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = colorFondo;
        cam.cullingMask = capasVisibles;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = alturaCamara + 250f;
        cam.allowMSAA = false;
        cam.allowHDR = false;
        cam.depth = -10f;
        cam.targetTexture = rt;
    }

    void CrearInterfaz()
    {
        GameObject cgo = new GameObject("CanvasMinimapa");
        cgo.transform.SetParent(transform, false);

        canvas = cgo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -1;   // Detrás del HUD y de los paneles del juego

        CanvasScaler scaler = cgo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // Marco
        root = NuevoRect("Minimapa", cgo.transform);
        Image marco = root.gameObject.AddComponent<Image>();
        marco.color = new Color(0.04f, 0.05f, 0.05f, 0.92f);
        marco.raycastTarget = false;

        // Imagen de la cámara
        RectTransform mapa = NuevoRect("Mapa", root);
        Estirar(mapa, BORDE);
        RawImage raw = mapa.gameObject.AddComponent<RawImage>();
        raw.texture = rt;
        raw.raycastTarget = false;

        // Capa de iconos (recorta lo que se sale del cuadro)
        iconos = NuevoRect("Iconos", root);
        Estirar(iconos, BORDE);
        iconos.gameObject.AddComponent<RectMask2D>();

        anillo = CrearIcono(iconos, "MasCercana", sprAnillo, new Color(1f, 1f, 1f, 0.95f));
        flecha = CrearIcono(iconos, "Dron", sprFlecha, cDron);

        // Barra de texto inferior
        RectTransform barra = NuevoRect("Barra", root);
        barra.anchorMin = new Vector2(0f, 0f);
        barra.anchorMax = new Vector2(1f, 0f);
        barra.pivot = new Vector2(0.5f, 0f);
        barra.offsetMin = new Vector2(BORDE, BORDE);
        barra.offsetMax = new Vector2(-BORDE, BORDE + ALTO_BARRA);
        Image fondoBarra = barra.gameObject.AddComponent<Image>();
        fondoBarra.color = new Color(0f, 0f, 0f, 0.55f);
        fondoBarra.raycastTarget = false;

        RectTransform txt = NuevoRect("Texto", barra);
        Estirar(txt, 2f);
        etiqueta = txt.gameObject.AddComponent<TextMeshProUGUI>();
        etiqueta.alignment = TextAlignmentOptions.Center;
        etiqueta.enableAutoSizing = true;
        etiqueta.fontSizeMin = 8f;
        etiqueta.fontSizeMax = 16f;
        etiqueta.color = Color.white;
        etiqueta.richText = true;
        etiqueta.raycastTarget = false;
    }

    static RectTransform NuevoRect(string nombre, Transform padre)
    {
        GameObject go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        return (RectTransform)go.transform;
    }

    static void Estirar(RectTransform r, float margen)
    {
        r.anchorMin = Vector2.zero;
        r.anchorMax = Vector2.one;
        r.offsetMin = new Vector2(margen, margen);
        r.offsetMax = new Vector2(-margen, -margen);
    }

    Image CrearIcono(Transform padre, string nombre, Sprite sprite, Color color)
    {
        RectTransform r = NuevoRect(nombre, padre);
        r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f);
        Image im = r.gameObject.AddComponent<Image>();
        im.sprite = sprite;   // null = cuadrado sólido
        im.color = color;
        im.raycastTarget = false;
        return im;
    }

    Image Icono(List<Image> pool, int i, Sprite sprite, string nombre)
    {
        while (pool.Count <= i) pool.Add(CrearIcono(iconos, nombre, sprite, Color.white));
        Image im = pool[i];
        if (!im.gameObject.activeSelf) im.gameObject.SetActive(true);
        return im;
    }

    static void OcultarDesde(List<Image> pool, int desde)
    {
        for (int i = desde; i < pool.Count; i++)
        {
            if (pool[i].gameObject.activeSelf) pool[i].gameObject.SetActive(false);
        }
    }

    void AplicarModo()
    {
        tamanoActual = mapaGrande ? tamanoGrande : tamanoPequeno;

        if (mapaGrande)
        {
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = new Vector2(0f, 20f);
        }
        else
        {
            bool derecha = esquina == Esquina.SuperiorDerecha || esquina == Esquina.InferiorDerecha;
            bool arriba = esquina == Esquina.SuperiorDerecha || esquina == Esquina.SuperiorIzquierda;
            Vector2 a = new Vector2(derecha ? 1f : 0f, arriba ? 1f : 0f);
            root.anchorMin = root.anchorMax = root.pivot = a;
            root.anchoredPosition = new Vector2(derecha ? -margenPantalla : margenPantalla,
                                                arriba ? -margenPantalla : margenPantalla);
        }

        root.sizeDelta = new Vector2(tamanoActual, tamanoActual);
        proximaEtiqueta = 0f;
    }

    // ==================== Bucle principal ====================

    void LateUpdate()
    {
        if (drone == null)
        {
            drone = FindFirstObjectByType<PlayerDrone>();
            if (drone == null) return;
        }

        bool jugando = GameManager.Instance == null || GameManager.Instance.EstaJugando;
        canvas.enabled = jugando;
        cam.enabled = jugando;
        if (!jugando) return;

        if (Keyboard.current != null && Keyboard.current[teclaMapaGrande].wasPressedThisFrame)
        {
            mapaGrande = !mapaGrande;
            AplicarModo();
        }

        if (Time.unscaledTime >= proximoRefresco)
        {
            proximoRefresco = Time.unscaledTime + 0.4f;
            RefrescarListas();
        }

        ActualizarCamara();
        ActualizarIconos();
    }

    void RefrescarListas()
    {
        basuras.Clear();
        foreach (GameObject go in GameObject.FindGameObjectsWithTag("Trash"))
        {
            TrashItem ti = go.GetComponentInParent<TrashItem>();
            basuras.Add(new Basura
            {
                t = go.transform,
                tipo = ti != null ? ti.tipo : TipoBasura.Plastico
            });
        }

        contenedores.Clear();
        foreach (ContenedorReciclaje c in FindObjectsByType<ContenedorReciclaje>(FindObjectsSortMode.None))
        {
            contenedores.Add(new Contenedor
            {
                t = c.transform,
                color = c.aceptaTodo ? Color.white : ColorTipo(c.tipoAceptado)
            });
        }

        terminales.Clear();
        foreach (TerminalMejoras t in FindObjectsByType<TerminalMejoras>(FindObjectsSortMode.None))
        {
            terminales.Add(t.transform);
        }
    }

    void ActualizarCamara()
    {
        Vector3 dp = drone.transform.position;

        Vector3 centro;
        if (mapaGrande)
        {
            centro = new Vector3(centroMundo.x, 0f, centroMundo.y);
            camYaw = 0f;
            cam.orthographicSize = Mathf.Max(10f, drone.limiteMapa * 1.08f);
        }
        else
        {
            centro = dp;
            camYaw = rotarConDron ? drone.transform.eulerAngles.y : 0f;
            cam.orthographicSize = zoomPequeno;
        }

        cam.transform.position = new Vector3(centro.x, alturaCamara, centro.z);
        cam.transform.rotation = Quaternion.Euler(90f, camYaw, 0f);
    }

    void ActualizarIconos()
    {
        float k = mapaGrande ? 1.5f : 1f;
        float mitad = (tamanoActual - 2f * BORDE) * 0.5f;
        float pxPorMetro = mitad / cam.orthographicSize;
        float limite = mitad - 6f * k;

        Vector3 origen = cam.transform.position;
        Vector3 r = cam.transform.right;
        Vector3 u = cam.transform.up;
        Vector3 dp = drone.transform.position;

        // --- Dron ---
        Vector2 pd = Sujetar(Proyectar(dp, origen, r, u, pxPorMetro), limite);
        flecha.rectTransform.anchoredPosition = pd;
        flecha.rectTransform.sizeDelta = Vector2.one * 26f * k;
        flecha.rectTransform.localRotation =
            Quaternion.Euler(0f, 0f, -Mathf.DeltaAngle(camYaw, drone.transform.eulerAngles.y));

        // --- Basura ---
        int n = 0;
        int total = 0;
        bool hayCercana = false;
        float mejor2 = float.MaxValue;
        Vector3 mejorPos = Vector3.zero;

        for (int i = 0; i < basuras.Count; i++)
        {
            Transform t = basuras[i].t;
            if (t == null) continue;

            Vector3 p = t.position;
            total++;

            float dx = p.x - dp.x, dz = p.z - dp.z;
            float d2 = dx * dx + dz * dz;
            if (d2 < mejor2) { mejor2 = d2; mejorPos = p; hayCercana = true; }

            Vector2 pos = Proyectar(p, origen, r, u, pxPorMetro);
            if (Mathf.Abs(pos.x) > limite || Mathf.Abs(pos.y) > limite) continue;

            Image ic = Icono(iconosBasura, n++, sprCirculo, "Basura");
            ic.color = ColorTipo(basuras[i].tipo);
            ic.rectTransform.anchoredPosition = pos;
            ic.rectTransform.sizeDelta = Vector2.one * 8f * k;
        }
        OcultarDesde(iconosBasura, n);

        // --- Contenedores (si quedan fuera, se pegan al borde) ---
        int c = 0;
        for (int i = 0; i < contenedores.Count; i++)
        {
            Transform t = contenedores[i].t;
            if (t == null) continue;

            Vector2 pos = Proyectar(t.position, origen, r, u, pxPorMetro);
            bool fuera = Mathf.Abs(pos.x) > limite || Mathf.Abs(pos.y) > limite;

            Image ic = Icono(iconosContenedor, c++, null, "Contenedor");
            Color col = contenedores[i].color;
            col.a = fuera ? 0.65f : 1f;
            ic.color = col;
            ic.rectTransform.anchoredPosition = Sujetar(pos, limite);
            ic.rectTransform.sizeDelta = Vector2.one * 13f * k;
        }
        OcultarDesde(iconosContenedor, c);

        // --- Terminal de mejoras ---
        int m = 0;
        for (int i = 0; i < terminales.Count; i++)
        {
            Transform t = terminales[i];
            if (t == null) continue;

            Vector2 pos = Proyectar(t.position, origen, r, u, pxPorMetro);
            bool fuera = Mathf.Abs(pos.x) > limite || Mathf.Abs(pos.y) > limite;

            Image ic = Icono(iconosTerminal, m++, null, "Terminal");
            Color col = cTerminal;
            col.a = fuera ? 0.65f : 1f;
            ic.color = col;
            ic.rectTransform.anchoredPosition = Sujetar(pos, limite);
            ic.rectTransform.sizeDelta = Vector2.one * 12f * k;
            ic.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
        }
        OcultarDesde(iconosTerminal, m);

        // --- Anillo de la basura más cercana ---
        if (hayCercana)
        {
            Vector2 pos = Proyectar(mejorPos, origen, r, u, pxPorMetro);
            bool fuera = Mathf.Abs(pos.x) > limite || Mathf.Abs(pos.y) > limite;
            if (!anillo.gameObject.activeSelf) anillo.gameObject.SetActive(true);
            anillo.rectTransform.anchoredPosition = Sujetar(pos, limite);
            anillo.rectTransform.sizeDelta = Vector2.one * (fuera ? 24f : 20f) * k;
        }
        else if (anillo.gameObject.activeSelf)
        {
            anillo.gameObject.SetActive(false);
        }

        // La flecha del dron siempre encima de todo
        flecha.transform.SetAsLastSibling();

        // --- Texto ---
        if (Time.unscaledTime >= proximaEtiqueta)
        {
            proximaEtiqueta = Time.unscaledTime + 0.2f;
            ActualizarEtiqueta(total, hayCercana ? Mathf.Sqrt(mejor2) : -1f);
        }
    }

    void ActualizarEtiqueta(int total, float distanciaCercana)
    {
        string datos = total <= 0
            ? "Sin basura en el mapa"
            : $"Basura: {total} | Cerca: {Mathf.RoundToInt(distanciaCercana)} m";

        if (mapaGrande)
        {
            etiqueta.text =
                $"[M] Cerrar | <color=#{ColorUtility.ToHtmlStringRGB(cPlastico)}>Plastico</color> " +
                $"<color=#{ColorUtility.ToHtmlStringRGB(cMetal)}>Metal</color> " +
                $"<color=#{ColorUtility.ToHtmlStringRGB(cVidrio)}>Vidrio</color> | " +
                $"<color=#{ColorUtility.ToHtmlStringRGB(cTerminal)}>Terminal</color> | {datos}";
        }
        else
        {
            etiqueta.text = $"[M] Mapa | {datos}";
        }
    }

    // ==================== Utilidades ====================

    static Vector2 Proyectar(Vector3 mundo, Vector3 origen, Vector3 right, Vector3 up, float pxPorMetro)
    {
        float dx = mundo.x - origen.x;
        float dz = mundo.z - origen.z;
        return new Vector2(dx * right.x + dz * right.z, dx * up.x + dz * up.z) * pxPorMetro;
    }

    static Vector2 Sujetar(Vector2 p, float limite)
    {
        float mayor = Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y));
        if (mayor > limite) p *= limite / mayor;
        return p;
    }

    static Color ColorTipo(TipoBasura tipo)
    {
        switch (tipo)
        {
            case TipoBasura.Metal: return cMetal;
            case TipoBasura.Vidrio: return cVidrio;
            default: return cPlastico;
        }
    }

    static Sprite CrearSpriteCirculo(int n, bool soloAnillo)
    {
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        float c = (n - 1) * 0.5f;
        float radio = n * 0.5f - 1f;
        float grosor = n * 0.16f;
        Color[] px = new Color[n * n];

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(radio - d + 0.5f);
                if (soloAnillo) a = Mathf.Min(a, Mathf.Clamp01(d - (radio - grosor) + 0.5f));
                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite CrearSpriteFlecha(int n)
    {
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Vector2 punta = new Vector2(0.50f, 0.97f);
        Vector2 izq = new Vector2(0.10f, 0.05f);
        Vector2 muesca = new Vector2(0.50f, 0.30f);
        Vector2 der = new Vector2(0.90f, 0.05f);

        const int sub = 3;   // supersampling para bordes suaves
        Color[] px = new Color[n * n];

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                int dentro = 0;
                for (int sy = 0; sy < sub; sy++)
                {
                    for (int sx = 0; sx < sub; sx++)
                    {
                        Vector2 p = new Vector2((x + (sx + 0.5f) / sub) / n, (y + (sy + 0.5f) / sub) / n);
                        if (EnTriangulo(p, punta, izq, muesca) || EnTriangulo(p, punta, muesca, der)) dentro++;
                    }
                }
                px[y * n + x] = new Color(1f, 1f, 1f, dentro / (float)(sub * sub));
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    static bool EnTriangulo(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Signo(p, a, b);
        float d2 = Signo(p, b, c);
        float d3 = Signo(p, c, a);
        bool hayNeg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool hayPos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(hayNeg && hayPos);
    }

    static float Signo(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }

    void OnDestroy()
    {
        if (rt != null)
        {
            rt.Release();
            Destroy(rt);
        }
    }
}
