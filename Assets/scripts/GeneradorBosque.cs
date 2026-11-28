using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class GeneradorBosque : MonoBehaviour
{
    [Header("1. Qué colocar y cuántos")]
    public GameObject[] prefabsVegetacion;
    public int cantidad = 40;
    public Vector2 rangoEscala = new Vector2(0.85f, 1.3f);
    public float distanciaMinimaEntreObjetos = 2.5f;

    [Header("2. Área y ajuste al terreno")]
    public float radioMapa = 30f;
    public float alturaInicioRayo = 100f;
    public float hundirEnSuelo = 0.15f;
    [Range(0f, 60f)] public float inclinacionMaxima = 35f;
    [Range(0f, 1f)] public float adaptarAPendiente = 0f; // 0 = recto (árboles), 1 = pegado a la loma (pasto/rocas)
    public LayerMask capaSuelo;

    [Header("3. Zona libre (Estación / Caminos)")]
    public Transform centroEstacion;
    public float radioExclusionEstacion = 8f;

    [ContextMenu("Generar Vegetación Ahora")]
    public void Generar()
    {
        if (prefabsVegetacion == null || prefabsVegetacion.Length == 0)
        {
            Debug.LogWarning("Asigna al menos un prefab en 'Prefabs Vegetacion'.");
            return;
        }

        // Registra lo que ya existe dentro de esta carpeta para no encimar nuevos objetos
        List<Vector3> posicionesUsadas = new List<Vector3>();
        foreach (Transform hijo in transform)
        {
            posicionesUsadas.Add(hijo.position);
        }

        int colocados = 0;
        int intentos = 0;
        int maxIntentos = cantidad * 15;

        while (colocados < cantidad && intentos < maxIntentos)
        {
            intentos++;

            float x = transform.position.x + Random.Range(-radioMapa, radioMapa);
            float z = transform.position.z + Random.Range(-radioMapa, radioMapa);

            // 1. Evita la zona de la estación
            if (centroEstacion != null)
            {
                Vector2 pCand = new Vector2(x, z);
                Vector2 pEst = new Vector2(centroEstacion.position.x, centroEstacion.position.z);
                if (Vector2.Distance(pCand, pEst) < radioExclusionEstacion)
                    continue;
            }

            // 2. Lanza el rayo hacia abajo buscando solo la capa Suelo
            Vector3 origen = new Vector3(x, transform.position.y + alturaInicioRayo, z);
            if (!Physics.Raycast(origen, Vector3.down, out RaycastHit hit, alturaInicioRayo * 2f, capaSuelo))
                continue;

            // 3. Descarta laderas demasiado empinadas
            if (Vector3.Angle(hit.normal, Vector3.up) > inclinacionMaxima)
                continue;

            // 4. Comprueba que no quede pegado a otro árbol ya colocado
            bool muyCerca = false;
            foreach (Vector3 pos in posicionesUsadas)
            {
                if (Vector3.Distance(hit.point, pos) < distanciaMinimaEntreObjetos)
                {
                    muyCerca = true;
                    break;
                }
            }
            if (muyCerca) continue;

            // 5. Calcula posición, rotación (opcionalmente inclinada) y escala
            GameObject prefab = prefabsVegetacion[Random.Range(0, prefabsVegetacion.Length)];
            Vector3 posicionFinal = hit.point - Vector3.up * hundirEnSuelo;

            Quaternion rotY = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            Quaternion rotPendiente = Quaternion.FromToRotation(Vector3.up, hit.normal);
            Quaternion rotacionFinal = Quaternion.Slerp(Quaternion.identity, rotPendiente, adaptarAPendiente) * rotY;

            GameObject nuevo = null;
#if UNITY_EDITOR
            nuevo = (GameObject)PrefabUtility.InstantiatePrefab(prefab, transform);
#endif
            if (nuevo == null)
            {
                nuevo = Instantiate(prefab, transform);
            }

            nuevo.transform.position = posicionFinal;
            nuevo.transform.rotation = rotacionFinal;
            nuevo.transform.localScale *= Random.Range(rangoEscala.x, rangoEscala.y);

            posicionesUsadas.Add(hit.point);
            colocados++;
        }

        Debug.Log($"GeneradorBosque: se colocaron {colocados}/{cantidad} objetos en '{name}'.");
    }

    [ContextMenu("Borrar Toda la Vegetación Hija")]
    public void Limpiar()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(transform.GetChild(i).gameObject);
        }
        Debug.Log($"Se limpió toda la vegetación dentro de '{name}'.");
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, new Vector3(radioMapa * 2f, 2f, radioMapa * 2f));

        if (centroEstacion != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(centroEstacion.position, radioExclusionEstacion);
        }
    }
}