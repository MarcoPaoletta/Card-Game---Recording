using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class BuilderActionButton : MonoBehaviour
{
    public enum Action
    {
        PrevLevel,
        NextLevel,
        ToggleBuilderMode,
        CreateNewLevel,
        DeleteCurrentLevel,
        OpenLevelsPanel,
        CloseLevelsPanel,
    }

    [SerializeField] private Action action;
    [SerializeField] private LevelBuilderManager manager;
    [SerializeField] private LevelsPanelView levelsPanel;

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Invoke);
    }

    void Invoke()
    {
        switch (action)
        {
            case Action.PrevLevel:          manager?.PrevLevel(); break;
            case Action.NextLevel:          manager?.NextLevel(); break;
            case Action.ToggleBuilderMode:  manager?.ToggleBuilderMode(); break;
            case Action.CreateNewLevel:     manager?.CreateNewLevel(); break;
            case Action.DeleteCurrentLevel: manager?.DeleteCurrentLevel(); break;
            case Action.OpenLevelsPanel:    levelsPanel?.Open(); break;
            case Action.CloseLevelsPanel:   levelsPanel?.Close(); break;
        }
    }
}
