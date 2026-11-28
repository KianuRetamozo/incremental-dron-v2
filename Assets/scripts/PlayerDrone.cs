using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PlayerDrone : MonoBehaviour
{
    [Header("Cámara Primera Persona")]
    public Transform camaraDron;
    public float sensibilidadMouse = 0.15f;
    public float limiteMiradaVertical = 80f;

    [Header("Puntero (Crosshair)")]
    public Image punteroUI;
    public float alcanceClick = 5f;
    public Color colorNormal = Color.white;
    public Color colorBasura = Color.green;
    public Color colorInteractuar = Color.cyan;
    public Color colorLleno = Color.red;

    [Header("Vuelo y Límites sobre Terreno Irregular")]
    public float suavizadoVuelo = 6f;
    public float velocidadMaxima = 26f;        // Permite aprovechar el Super Turbo
    public float limiteMapa = 45f;
    public float alturaMinima = 1.0f;          // Margen mínimo cómodo sobre el terreno
    public float margenSeguridadSuelo = 0.4f;  // Tope duro para no atravesar el pasto
    public float alturaMaxima = 25f;
    public float suavizadoTerreno = 12f;
    public LayerMask capaSuelo;

    [Header("Inventario Único, Imán y Descarga")]
    public List<TipoBasura> inventario = new List<TipoBasura>();
    public float velocidadAtraccionIman = 14f;
    public float distanciaCapturaIman = 1.35f;
    public float radioDescargaAutomatica = 5.5f;

    private float rotacionX = 0f;
    private float temporizadorDescarga = 0f;
    private Vector3 velocidadActual = Vector3.zero;

    void Start()
    {
        if (camaraDron == null)
        {
            Camera cam = GetComponentInChildren<Camera>();
            if (cam != null) camaraDron = cam.transform;
        }
    }

    void Update()
    {
        if (GameManager.Instance == null || !GameManager.Instance.EstaJugando)
        {
            velocidadActual = Vector3.zero;
            return;
        }

        RotarCamara();
        MoverDron();
        ManejarInteraccionYPuntero();
        ManejarDescargaAutomatica();
    }

    void ManejarInteraccionYPuntero()
    {
        bool estaLleno = inventario.Count >= GameManager.Instance.capacidadDron;

        if (!estaLleno && GameManager.Instance.EsRecoleccionAutomatica)
        {
            RecogerBasuraAutomatica();
            estaLleno = inventario.Count >= GameManager.Instance.capacidadDron;
        }

        if (camaraDron == null) return;

        float distancia = Mathf.Max(alcanceClick, GameManager.Instance.radioAspiracion);
        Ray rayo = new Ray(camaraDron.position, camaraDron.forward);

        if (Physics.Raycast(rayo, out RaycastHit impacto, distancia))
        {
            // A) Terminal de Mejoras
            if (impacto.collider.GetComponentInParent<TerminalMejoras>() != null)
            {
                CambiarColorPuntero(colorInteractuar);
                GameManager.Instance.MostrarInteraccion("[Clic] Abrir Terminal de Mejoras");

                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    GameManager.Instance.BotonAbrirMejoras();
                }
                return;
            }

            // B) Contenedor de Reciclaje
            ContenedorReciclaje contenedor = impacto.collider.GetComponentInParent<ContenedorReciclaje>();
            if (contenedor != null)
            {
                CambiarColorPuntero(colorInteractuar);
                bool esUniversal = contenedor.aceptaTodo || GameManager.Instance.ClasificadorUniversal;
                string nombreContenedor = esUniversal ? "Todo (Universal)" : contenedor.tipoAceptado.ToString();
                GameManager.Instance.MostrarInteraccion($"[Mantener Clic] Depositar {nombreContenedor}");

                float cadencia = GameManager.Instance.VelocidadDescargaActual;
                if (Mouse.current != null && Mouse.current.leftButton.isPressed)
                {
                    temporizadorDescarga += Time.deltaTime;
                    if (temporizadorDescarga >= cadencia)
                    {
                        temporizadorDescarga = 0f;
                        IntentarDescargarUno(contenedor);
                    }
                }
                else
                {
                    temporizadorDescarga = cadencia;
                }
                return;
            }

            // C) Basura
            if (impacto.collider.CompareTag("Trash"))
            {
                TrashItem item = impacto.collider.GetComponentInParent<TrashItem>();
                string nombreBasura = item != null ? item.tipo.ToString() : "Basura";

                if (estaLleno)
                {
                    CambiarColorPuntero(colorLleno);
                    GameManager.Instance.MostrarInteraccion("¡Carga llena! Ve a los contenedores");
                    return;
                }

                CambiarColorPuntero(colorBasura);
                GameManager.Instance.MostrarInteraccion($"[Clic] Recoger {nombreBasura}");

                if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    RecogerObjeto(impacto.collider.gameObject);
                }
                return;
            }
        }

        // Sin objetivo bajo el puntero
        CambiarColorPuntero(estaLleno ? colorLleno : colorNormal);
        GameManager.Instance.MostrarInteraccion(estaLleno ? "Carga llena — Regresa a la estación" : "");
    }

    void IntentarDescargarUno(ContenedorReciclaje contenedor)
    {
        if (inventario.Count == 0)
        {
            GameManager.Instance.MostrarFeedback("Tu carga está vacía", 1f);
            return;
        }

        bool aceptaCualquiera = contenedor.aceptaTodo || GameManager.Instance.ClasificadorUniversal;
        int indice = aceptaCualquiera
            ? inventario.Count - 1
            : inventario.FindIndex(item => item == contenedor.tipoAceptado);

        if (indice != -1)
        {
            TipoBasura basuraEntregada = inventario[indice];
            inventario.RemoveAt(indice);
            ContarMateriales(out int p, out int m, out int v);
            GameManager.Instance.DepositarUnMaterial(basuraEntregada, inventario.Count, p, m, v);
        }
        else
        {
            GameManager.Instance.MostrarFeedback($"No llevas {contenedor.tipoAceptado} en tu carga", 1.2f);
        }
    }

    void ManejarDescargaAutomatica()
    {
        if (inventario.Count == 0 || !GameManager.Instance.DescargaAutomaticaEstacion) return;

        Collider[] cercanos = Physics.OverlapSphere(transform.position, radioDescargaAutomatica);
        foreach (Collider col in cercanos)
        {
            ContenedorReciclaje cont = col.GetComponentInParent<ContenedorReciclaje>();
            if (cont != null)
            {
                temporizadorDescarga += Time.deltaTime;
                if (temporizadorDescarga >= GameManager.Instance.VelocidadDescargaActual)
                {
                    temporizadorDescarga = 0f;
                    IntentarDescargarUno(cont);
                }
                return;
            }
        }
    }

    void RecogerBasuraAutomatica()
    {
        Collider[] objetosCercanos = Physics.OverlapSphere(transform.position, GameManager.Instance.radioAspiracion);

        foreach (Collider col in objetosCercanos)
        {
            if (col == null || !col.CompareTag("Trash")) continue;
            if (inventario.Count >= GameManager.Instance.capacidadDron) break;

            // Atrae suavemente el desecho hacia el dron
            col.transform.position = Vector3.MoveTowards(
                col.transform.position,
                transform.position,
                velocidadAtraccionIman * Time.deltaTime
            );

            // Al llegar cerca, lo guarda en el inventario
            if (Vector3.Distance(col.transform.position, transform.position) <= distanciaCapturaIman)
            {
                RecogerObjeto(col.gameObject);
            }
        }
    }

    void RecogerObjeto(GameObject basura)
    {
        if (basura == null) return;

        TrashItem item = basura.GetComponentInParent<TrashItem>();
        TipoBasura tipo = item != null ? item.tipo : TipoBasura.Plastico;

        inventario.Add(tipo);
        Destroy(basura);

        ContarMateriales(out int p, out int m, out int v);
        GameManager.Instance.ActualizarCargaHUD(inventario.Count, p, m, v);
    }

    void ContarMateriales(out int p, out int m, out int v)
    {
        p = 0; m = 0; v = 0;
        foreach (TipoBasura item in inventario)
        {
            if (item == TipoBasura.Plastico) p++;
            else if (item == TipoBasura.Metal) m++;
            else if (item == TipoBasura.Vidrio) v++;
        }
    }

    void CambiarColorPuntero(Color c)
    {
        if (punteroUI != null) punteroUI.color = c;
    }

    void RotarCamara()
    {
        if (Mouse.current == null || camaraDron == null) return;

        Vector2 deltaMouse = Mouse.current.delta.ReadValue();
        transform.Rotate(Vector3.up * (deltaMouse.x * sensibilidadMouse));

        rotacionX -= deltaMouse.y * sensibilidadMouse;
        rotacionX = Mathf.Clamp(rotacionX, -limiteMiradaVertical, limiteMiradaVertical);
        camaraDron.localRotation = Quaternion.Euler(rotacionX, 0f, 0f);
    }

    void MoverDron()
    {
        float h = 0f, v = 0f, y = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) h -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) h += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) v -= 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) v += 1f;
            if (Keyboard.current.spaceKey.isPressed || Keyboard.current.eKey.isPressed) y += 1f;
            if (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.qKey.isPressed) y -= 1f;
        }

        Vector3 direccion = transform.right * h + transform.forward * v + Vector3.up * y;
        if (direccion.magnitude > 1f) direccion.Normalize();

        float velBase = GameManager.Instance != null ? GameManager.Instance.velocidadDron : 7f;
        if (GameManager.Instance != null && GameManager.Instance.TieneTurbo &&
            Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed)
        {
            velBase *= GameManager.Instance.MultiplicadorTurbo;
        }

        Vector3 velocidadObjetivo = direccion * Mathf.Min(velBase, velocidadMaxima);
        velocidadActual = Vector3.Lerp(velocidadActual, velocidadObjetivo, suavizadoVuelo * Time.deltaTime);

        Vector3 nuevaPos = transform.position + velocidadActual * Time.deltaTime;
        nuevaPos.x = Mathf.Clamp(nuevaPos.x, -limiteMapa, limiteMapa);
        nuevaPos.z = Mathf.Clamp(nuevaPos.z, -limiteMapa, limiteMapa);

        // Detecta la altura real del terreno irregular debajo del dron
        Vector3 origenRayo = new Vector3(nuevaPos.x, nuevaPos.y + 20f, nuevaPos.z);
        if (Physics.Raycast(origenRayo, Vector3.down, out RaycastHit hitSuelo, 200f, capaSuelo))
        {
            float alturaSuelo = hitSuelo.point.y;
            float pisoDinamico = alturaSuelo + alturaMinima;
            float topeDuro = alturaSuelo + margenSeguridadSuelo;

            if (nuevaPos.y < pisoDinamico)
            {
                nuevaPos.y = Mathf.Lerp(nuevaPos.y, pisoDinamico, suavizadoTerreno * Time.deltaTime);

                if (nuevaPos.y < topeDuro)
                {
                    nuevaPos.y = topeDuro;
                    if (velocidadActual.y < 0f) velocidadActual.y = 0f;
                }
            }

            float techoSeguro = Mathf.Max(alturaMaxima, pisoDinamico + 3f);
            if (nuevaPos.y > techoSeguro)
            {
                nuevaPos.y = techoSeguro;
                if (velocidadActual.y > 0f) velocidadActual.y = 0f;
            }
        }
        else
        {
            nuevaPos.y = Mathf.Clamp(nuevaPos.y, alturaMinima, alturaMaxima);
        }

        transform.position = nuevaPos;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        float radio = GameManager.Instance != null ? GameManager.Instance.radioAspiracion : 1.8f;
        Gizmos.DrawWireSphere(transform.position, radio);
    }
}