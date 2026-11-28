using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Música de fondo. Se crea SOLA al iniciar el juego (no hay que configurar nada en la escena).
///
/// CÓMO USARLA:
///   1) Crea la carpeta  Assets/Resources/Musica  (con ese nombre exacto).
///   2) Mete ahí tus canciones (.mp3, .ogg, .wav).
///   3) Dale Play. Suenan todas en orden aleatorio, una tras otra, en bucle.
///
/// TECLAS:  N = siguiente canción   |   B = silenciar / activar música
///
/// Extras: fundido suave al empezar y al terminar cada canción, y baja un poco el volumen
/// cuando estás en el menú, en pausa o en la terminal de mejoras. No se corta al reiniciar.
///
/// Si prefieres elegir las canciones a mano: pon este script en un GameObject vacío de la
/// escena y arrastra los clips a la lista "Canciones" (así ya no se usa la carpeta Resources).
/// </summary>
public class MusicaFondo : MonoBehaviour
{
    [Header("Canciones")]
    [Tooltip("Opcional. Si lo dejas vacío, carga todo lo que haya en Assets/Resources/Musica")]
    public AudioClip[] canciones;
    public string carpetaResources = "Musica";
    public bool mezclarOrden = true;

    [Header("Volumen")]
    [Range(0f, 1f)] public float volumen = 0.35f;
    [Tooltip("Qué tanto baja el volumen en menú, pausa y terminal (0.5 = a la mitad).")]
    [Range(0f, 1f)] public float factorEnMenus = 0.5f;
    [Tooltip("Segundos de fundido al empezar y al terminar cada canción.")]
    public float segundosFundido = 2f;

    [Header("Teclas")]
    public Key teclaSiguiente = Key.N;
    public Key teclaSilenciar = Key.B;

    static MusicaFondo instancia;

    AudioSource fuente;
    readonly List<AudioClip> lista = new List<AudioClip>();
    int indice = -1;
    bool silenciado;
    float volumenActual;

    // ==================== Arranque automático ====================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Iniciar()
    {
        if (instancia != null) return;
        if (FindFirstObjectByType<MusicaFondo>() != null) return;
        new GameObject("MusicaFondo").AddComponent<MusicaFondo>();
    }

    void Awake()
    {
        // Solo una instancia, y que sobreviva al reinicio de escena
        if (instancia != null && instancia != this)
        {
            Destroy(gameObject);
            return;
        }
        instancia = this;
        DontDestroyOnLoad(gameObject);

        fuente = gameObject.AddComponent<AudioSource>();
        fuente.playOnAwake = false;
        fuente.loop = false;
        fuente.spatialBlend = 0f;   // Sonido 2D: se oye igual en todas partes
        fuente.volume = 0f;

        CargarCanciones();
    }

    void CargarCanciones()
    {
        lista.Clear();

        if (canciones != null && canciones.Length > 0)
        {
            foreach (AudioClip c in canciones)
            {
                if (c != null) lista.Add(c);
            }
        }
        else
        {
            AudioClip[] cargadas = Resources.LoadAll<AudioClip>(carpetaResources);
            lista.AddRange(cargadas);
        }

        if (lista.Count == 0)
        {
            Debug.LogWarning("MusicaFondo: no encontré canciones. Crea la carpeta Assets/Resources/" +
                             carpetaResources + " y mete ahí tus .mp3/.ogg/.wav.");
            enabled = false;
            return;
        }

        if (mezclarOrden) Mezclar();
        indice = -1;
        ReproducirSiguiente();
    }

    void Mezclar()
    {
        for (int i = lista.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            AudioClip tmp = lista[i];
            lista[i] = lista[j];
            lista[j] = tmp;
        }
    }

    // ==================== Bucle ====================

    void Update()
    {
        if (lista.Count == 0) return;

        if (Keyboard.current != null)
        {
            if (Keyboard.current[teclaSiguiente].wasPressedThisFrame)
            {
                ReproducirSiguiente();
                if (GameManager.Instance != null && fuente.clip != null)
                {
                    GameManager.Instance.MostrarFeedback("Musica: " + fuente.clip.name, 2f);
                }
            }

            if (Keyboard.current[teclaSilenciar].wasPressedThisFrame)
            {
                silenciado = !silenciado;
            }
        }

        // Cuando termina la canción, pasa a la siguiente
        // (no avanza si la ventana está en segundo plano, para no saltarse canciones)
        if (!fuente.isPlaying && Application.isFocused)
        {
            ReproducirSiguiente();
        }

        ActualizarVolumen();
    }

    void ReproducirSiguiente()
    {
        indice = (indice + 1) % lista.Count;

        // Al dar la vuelta completa, vuelve a mezclar (y evita repetir la misma seguida)
        if (indice == 0 && mezclarOrden && lista.Count > 1)
        {
            AudioClip ultima = fuente.clip;
            Mezclar();
            if (lista[0] == ultima)
            {
                AudioClip tmp = lista[0];
                lista[0] = lista[lista.Count - 1];
                lista[lista.Count - 1] = tmp;
            }
        }

        fuente.Stop();
        fuente.clip = lista[indice];
        volumenActual = 0f;     // Fundido de entrada
        fuente.volume = 0f;
        fuente.Play();
    }

    void ActualizarVolumen()
    {
        bool jugando = GameManager.Instance == null || GameManager.Instance.EstaJugando;
        float objetivo = silenciado ? 0f : volumen * (jugando ? 1f : factorEnMenus);

        // Fundido de salida en los últimos segundos de la canción
        if (fuente.clip != null && fuente.isPlaying && segundosFundido > 0.01f)
        {
            float restante = fuente.clip.length - fuente.time;
            if (restante < segundosFundido) objetivo *= Mathf.Clamp01(restante / segundosFundido);
        }

        // Unscaled: sigue funcionando aunque el juego esté en pausa (timeScale = 0)
        float velocidad = 1f / Mathf.Max(0.01f, segundosFundido);
        volumenActual = Mathf.MoveTowards(volumenActual, objetivo, velocidad * Time.unscaledDeltaTime);
        fuente.volume = volumenActual;
    }
}
