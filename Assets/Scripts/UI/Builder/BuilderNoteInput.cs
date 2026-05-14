using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_InputField))]
public class BuilderNoteInput : MonoBehaviour
{
    [SerializeField] private LevelBuilderManager manager;

    private TMP_InputField input;

    void Awake()
    {
        input = GetComponent<TMP_InputField>();
        input.onEndEdit.AddListener(s => manager?.SetCurrentNote(s));
    }

    public void Refresh()
    {
        if (manager == null) return;
        input.SetTextWithoutNotify(manager.GetCurrentNote());
    }
}
