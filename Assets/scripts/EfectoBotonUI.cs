using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Efecto de escala al pasar el mouse / presionar. Usa tiempo sin escala (funciona con el juego en pausa).</summary>
public class EfectoBotonUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    public float escalaHover = 1.06f;
    public float escalaPresionado = 0.96f;

    float objetivo = 1f;
    bool encima;
    Button boton;

    void Awake()
    {
        boton = GetComponent<Button>();
    }

    void OnDisable()
    {
        encima = false;
        objetivo = 1f;
        transform.localScale = Vector3.one;
    }

    void Update()
    {
        float s = Mathf.Lerp(transform.localScale.x, objetivo, 18f * Time.unscaledDeltaTime);
        transform.localScale = new Vector3(s, s, 1f);
    }

    bool Activo()
    {
        return boton == null || boton.interactable;
    }

    public void OnPointerEnter(PointerEventData e)
    {
        encima = true;
        objetivo = Activo() ? escalaHover : 1f;
    }

    public void OnPointerExit(PointerEventData e)
    {
        encima = false;
        objetivo = 1f;
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (Activo()) objetivo = escalaPresionado;
    }

    public void OnPointerUp(PointerEventData e)
    {
        objetivo = encima && Activo() ? escalaHover : 1f;
    }
}
