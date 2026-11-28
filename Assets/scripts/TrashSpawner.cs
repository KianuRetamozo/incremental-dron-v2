using System.Collections.Generic;
using UnityEngine;

public class TrashSpawner : MonoBehaviour
{
    [Header("1. Prefabs por Tipo (para garantizar variedad en el inicio)")]
    public GameObject prefabPlastico;
    public GameObject prefabMetal;
    public GameObject prefabVidrio;

    [Header("2. Zona de Demostración (cerca del Spawn/Estación)")]
    public Transform centroEstacion;
    public float radioExclusionEstacion = 8f;   // Círculo rojo: nada aparece aquí
    public float radioZonaDemo = 20f;           // Círculo celeste: basura garantizada al inicio
    public int plasticoEnDemo = 4;
    public int metalEnDemo = 4;
    public int vidrioEnDemo = 4;

    [Header("3. Spawn Aleatorio en Todo el Mapa")]
    public Transform centroMapa;                // Opcional: arrastra LPW_Ground_A1_100x100_01
    public float radioMapa = 45f;               // Cuadro verde
    public int basuraInicialMapa = 45;
    public int maxBasuraSimultanea = 60;
    public int limiteTotalPartida = 120;        // 0 = infinito; > 0 = cantidad finita en toda la partida
    public float segundosEntreSpawns = 1.5f;

    [Header("4. Anti-Incrustación (Relieve y Árboles)")]
    public LayerMask capaSuelo;                 // Marca SOLO "Suelo"
    public LayerMask capaObstaculos;            // Marca "Default" / "Obstaculos" (TODO menos Suelo)
    public float alturaInicioRayo = 100f;
    public float alturaSobreSuelo = 0.25f;
    [Range(0f, 60f)] public float inclinacionMaxima = 32f;
    [Range(0f, 1f)] public float adaptarAPendiente = 0.8f;
    public float radioLibreObstaculos = 1.2f;   // Distancia mínima a troncos, rocas o contenedores
    public float distanciaMinEntreBasuras = 1.5f;

    private float temporizador;
    private int totalGenerado = 0;
    private List<GameObject> listaPrefabs = new List<GameObject>();

    void Start()
    {
        PrepararListaPrefabs();
        if (listaPrefabs.Count == 0)
        {
            Debug.LogWarning("TrashSpawner: asigna los prefabs de Plástico, Metal y Vidrio.");
            return;
        }

        // 1) Genera la basura garantizada cerca de la estación para probar mejoras
        GenerarLoteEnAnillo(prefabPlastico, plasticoEnDemo, radioExclusionEstacion, radioZonaDemo);
        GenerarLoteEnAnillo(prefabMetal, metalEnDemo, radioExclusionEstacion, radioZonaDemo);
        GenerarLoteEnAnillo(prefabVidrio, vidrioEnDemo, radioExclusionEstacion, radioZonaDemo);

        // 2) Distribuye el resto de la basura aleatoria por todo el terreno
        for (int i = 0; i < basuraInicialMapa; i++)
        {
            if (AlcanzoLimiteTotal()) break;
            IntentarGenerarEnMapa(ElegirPrefabAleatorio());
        }
    }

    void Update()
    {
        if (listaPrefabs.Count == 0 || AlcanzoLimiteTotal()) return;

        temporizador += Time.deltaTime;
        if (temporizador >= segundosEntreSpawns)
        {
            temporizador = 0f;
            if (GameObject.FindGameObjectsWithTag("Trash").Length < maxBasuraSimultanea)
            {
                IntentarGenerarEnMapa(ElegirPrefabAleatorio());
            }
        }
    }

    void PrepararListaPrefabs()
    {
        listaPrefabs.Clear();
        if (prefabPlastico != null) listaPrefabs.Add(prefabPlastico);
        if (prefabMetal != null) listaPrefabs.Add(prefabMetal);
        if (prefabVidrio != null) listaPrefabs.Add(prefabVidrio);
    }

    bool AlcanzoLimiteTotal()
    {
        return limiteTotalPartida > 0 && totalGenerado >= limiteTotalPartida;
    }

    GameObject ElegirPrefabAleatorio()
    {
        return listaPrefabs[Random.Range(0, listaPrefabs.Count)];
    }

    void GenerarLoteEnAnillo(GameObject prefab, int cantidad, float radioMin, float radioMax)
    {
        if (prefab == null || cantidad <= 0) return;

        Vector3 centro = centroEstacion != null ? centroEstacion.position : ObtenerCentroMapa();
        float rMax = Mathf.Max(radioMax, radioMin + 2f);

        for (int i = 0; i < cantidad; i++)
        {
            if (AlcanzoLimiteTotal()) break;

            for (int intento = 0; intento < 25; intento++)
            {
                float angulo = Random.Range(0f, Mathf.PI * 2f);
                float dist = Random.Range(radioMin, rMax);
                float x = centro.x + Mathf.Cos(angulo) * dist;
                float z = centro.z + Mathf.Sin(angulo) * dist;

                if (IntentarColocarEnPunto(prefab, x, z))
                    break;
            }
        }
    }

    bool IntentarGenerarEnMapa(GameObject prefab)
    {
        if (prefab == null) return false;

        Vector3 centro = ObtenerCentroMapa();
        for (int intento = 0; intento < 20; intento++)
        {
            float x = centro.x + Random.Range(-radioMapa, radioMapa);
            float z = centro.z + Random.Range(-radioMapa, radioMapa);

            if (EstaDentroDeEstacion(x, z)) continue;

            if (IntentarColocarEnPunto(prefab, x, z))
                return true;
        }
        return false;
    }

    bool IntentarColocarEnPunto(GameObject prefab, float x, float z)
    {
        Vector3 centro = ObtenerCentroMapa();
        Vector3 origenRayo = new Vector3(x, centro.y + alturaInicioRayo, z);

        // 1. Buscar la superficie real del terreno
        if (!Physics.Raycast(origenRayo, Vector3.down, out RaycastHit hit, alturaInicioRayo * 2f, capaSuelo))
            return false;

        // 2. Descartar pendientes muy inclinadas
        if (Vector3.Angle(hit.normal, Vector3.up) > inclinacionMaxima)
            return false;

        // Posición ligeramente elevada sobre el suelo siguiendo la normal
        Vector3 posFinal = hit.point + hit.normal * alturaSobreSuelo;

        // 3. Comprobar que no haya árboles, rocas, estación u otros obstáculos tocando esa esfera
        // Se eleva el centro de chequeo para no rozar el propio terreno
        Vector3 centroChequeo = hit.point + Vector3.up * (radioLibreObstaculos + 0.1f);
        if (capaObstaculos != 0 && Physics.CheckSphere(centroChequeo, radioLibreObstaculos, capaObstaculos))
            return false;

        // 4. Comprobar distancia mínima con otras basuras ya existentes
        Collider[] cercanos = Physics.OverlapSphere(posFinal, distanciaMinEntreBasuras);
        foreach (Collider col in cercanos)
        {
            if (col.CompareTag("Trash")) return false;
        }

        // Rotación aleatoria adaptada suavemente al relieve
        Quaternion rotY = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        Quaternion rotPendiente = Quaternion.FromToRotation(Vector3.up, hit.normal);
        Quaternion rotFinal = Quaternion.Slerp(Quaternion.identity, rotPendiente, adaptarAPendiente) * rotY;

        Instantiate(prefab, posFinal, rotFinal);
        totalGenerado++;
        return true;
    }

    bool EstaDentroDeEstacion(float x, float z)
    {
        if (centroEstacion == null) return false;
        Vector2 pCand = new Vector2(x, z);
        Vector2 pEst = new Vector2(centroEstacion.position.x, centroEstacion.position.z);
        return Vector2.Distance(pCand, pEst) < radioExclusionEstacion;
    }

    Vector3 ObtenerCentroMapa()
    {
        return centroMapa != null ? centroMapa.position : Vector3.zero;
    }

    void OnDrawGizmosSelected()
    {
        Vector3 centro = ObtenerCentroMapa();
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(centro, new Vector3(radioMapa * 2f, 2f, radioMapa * 2f));

        if (centroEstacion != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(centroEstacion.position, radioExclusionEstacion);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(centroEstacion.position, radioZonaDemo);
        }
    }
}