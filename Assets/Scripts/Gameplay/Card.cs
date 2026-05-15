using UnityEngine;

public class Card : MonoBehaviour
{
    [SerializeField] private Transform model;
    [SerializeField] private GameObject arrow;

    public void Setup(Color color, bool showArrow, Vector3 arrowWorldPos = default)
    {
        if (model != null)
        {
            var mr = model.GetComponentInChildren<MeshRenderer>();
            if (mr != null)
            {
                var mat = mr.materials[0];
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                else mat.color = color;
            }
        }

        if (arrow != null)
        {
            arrow.SetActive(showArrow);
            if (showArrow)
            {
                Vector3 cur = arrow.transform.position;
                arrow.transform.position = new Vector3(arrowWorldPos.x, cur.y, arrowWorldPos.z);
            }
        }
    }

    /// <summary>Oculta la flecha del chunk. Se llama cuando la carta sale del tablero.</summary>
    public void HideArrow()
    {
        if (arrow != null) arrow.SetActive(false);
    }
}
