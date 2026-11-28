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
}