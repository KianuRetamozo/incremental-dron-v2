using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sonidos de basura: al RECOGER (plástico, metal, vidrio) y al DEPOSITAR en el tacho.
/// Se crea SOLO al iniciar la escena. No hace falta tocar PlayerDrone: mira la carga del dron
/// y detecta cuándo entra o sale basura.
///
/// CÓMO USARLO:
///   1) Crea la carpeta  Assets/Resources/Sonidos  (con ese nombre exacto).
///   2) Mete ahí estos 4 archivos (nombre exacto, sin tildes ni espacios;
///      la extensión puede ser .wav, .mp3 u .ogg):
///          recoger_plastico
///          recoger_metal
///          recoger_vidrio
///          depositar_basura
///   3) Dale Play.
///
/// Si quieres elegir los audios a mano: pon este script en un GameObject vacío de la escena
/// y arrastra los clips a los campos de abajo (así tienen prioridad sobre la carpeta).
/// </summary>
public class SonidosRecoleccion : MonoBehaviour
{
    [Header("Audios (opcional: si los dejas vacíos se cargan desde Resources)")]
    public AudioClip sonidoPlastico;
    public AudioClip sonidoMetal;
    public AudioClip sonidoVidrio;
    public AudioClip sonidoDepositar;

    [Header("Carpeta dentro de Assets/Resources")]
    public string carpetaResources = "Sonidos";

    [Header("Volumen")]
    [Range(0f, 1f)] public float volumenRecoger = 0.8f;
    [Range(0f, 1f)] public float volumenDepositar = 0.8f;

    [Header("Ajustes")]
    [Tooltip("Cambia un poquito el tono cada vez para que no suene siempre igual (0 = sin variación).")]
    [Range(0f, 0.3f)] public float variacionTono = 0.06f;
    [Tooltip("Segundos mínimos entre dos sonidos de depositar (evita que se amontonen cuando descargas rápido).")]
    public float intervaloMinimoDeposito = 0.08f;

    AudioSource fuente;
    PlayerDrone drone;
    int pAnt, mAnt, vAnt;
    float proximoDeposito;

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
        if (FindFirstObjectByType<SonidosRecoleccion>() != null) return;
        new GameObject("SonidosRecoleccion").AddComponent<SonidosRecoleccion>();
    }

    // ==================== Lógica ====================

    void Awake()
    {
        fuente = gameObject.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.loop = false;
        fuente.spatialBlend = 0f;   // Sonido 2D: se oye igual siempre

        if (sonidoPlastico == null) sonidoPlastico = Cargar("recoger_plastico");
        if (sonidoMetal == null) sonidoMetal = Cargar("recoger_metal");
        if (sonidoVidrio == null) sonidoVidrio = Cargar("recoger_vidrio");
        if (sonidoDepositar == null) sonidoDepositar = Cargar("depositar_basura");
    }

    AudioClip Cargar(string nombre)
    {
        AudioClip clip = Resources.Load<AudioClip>(carpetaResources + "/" + nombre);
        if (clip == null)
        {
            Debug.LogWarning("SonidosRecoleccion: no encontré Assets/Resources/" +
                             carpetaResources + "/" + nombre + " (.wav/.mp3/.ogg)");
        }
        return clip;
    }

    void Update()
    {
        if (drone == null)
        {
            drone = FindFirstObjectByType<PlayerDrone>();
            if (drone == null) return;
            Contar(out pAnt, out mAnt, out vAnt);
            return;
        }

        Contar(out int p, out int m, out int v);

        // --- RECOGER: subió la cantidad de algún tipo ---
        // (un solo sonido por tipo y por frame, así el imán no suena a ruido)
        if (p > pAnt) Reproducir(sonidoPlastico, volumenRecoger);
        if (m > mAnt) Reproducir(sonidoMetal, volumenRecoger);
        if (v > vAnt) Reproducir(sonidoVidrio, volumenRecoger);

        // --- DEPOSITAR: bajó la cantidad de algún tipo (entregaste al tacho) ---
        bool bajo = p < pAnt || m < mAnt || v < vAnt;
        if (bajo && Time.time >= proximoDeposito)
        {
            proximoDeposito = Time.time + intervaloMinimoDeposito;
            Reproducir(sonidoDepositar, volumenDepositar);
        }

        pAnt = p;
        mAnt = m;
        vAnt = v;
    }

    void Contar(out int p, out int m, out int v)
    {
        p = 0; m = 0; v = 0;
        foreach (TipoBasura tipo in drone.inventario)
        {
            if (tipo == TipoBasura.Plastico) p++;
            else if (tipo == TipoBasura.Metal) m++;
            else if (tipo == TipoBasura.Vidrio) v++;
        }
    }

    void Reproducir(AudioClip clip, float volumen)
    {
        if (clip == null) return;
        fuente.pitch = 1f + Random.Range(-variacionTono, variacionTono);
        fuente.PlayOneShot(clip, volumen);
    }
}
