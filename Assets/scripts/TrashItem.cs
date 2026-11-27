using UnityEngine;

public enum TipoBasura
{
    Plastico,
    Metal,
    Vidrio
}

public class TrashItem : MonoBehaviour
{
    public TipoBasura tipo = TipoBasura.Plastico;

    /// <summary>Evita que el dron cuente dos veces el mismo objeto (varios colliders / Destroy diferido).</summary>
    [System.NonSerialized] public bool recogido;
}