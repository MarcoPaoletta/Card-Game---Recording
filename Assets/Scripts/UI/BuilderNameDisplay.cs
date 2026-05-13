using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class BuilderNameDisplay : MonoBehaviour
{
    [SerializeField] private LevelBuilderManager manager;

    private TMP_Text label;

    void Awake()
    {
        label = GetComponent<TMP_Text>();
    }

    public void Refresh()
    {
        if (manager == null || manager.LevelData == null) return;
        label.text = manager.LevelData.levelName;
    }
}
