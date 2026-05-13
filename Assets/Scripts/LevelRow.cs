using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelRow : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Button selectButton;
    [SerializeField] private Button upButton;
    [SerializeField] private Button downButton;
    [SerializeField] private Image background;

    public void Setup(
        string label,
        bool isCurrent,
        bool canMoveUp,
        bool canMoveDown,
        Action onSelect,
        Action onUp,
        Action onDown)
    {
        if (nameText != null) nameText.text = label;
        if (background != null)
            background.color = isCurrent ? new Color(0.18f, 0.36f, 0.55f, 1f) : new Color(0.15f, 0.15f, 0.18f, 1f);

        if (selectButton != null)
        {
            selectButton.onClick.RemoveAllListeners();
            selectButton.onClick.AddListener(() => onSelect?.Invoke());
        }
        if (upButton != null)
        {
            upButton.onClick.RemoveAllListeners();
            upButton.interactable = canMoveUp;
            upButton.onClick.AddListener(() => onUp?.Invoke());
        }
        if (downButton != null)
        {
            downButton.onClick.RemoveAllListeners();
            downButton.interactable = canMoveDown;
            downButton.onClick.AddListener(() => onDown?.Invoke());
        }
    }
}
