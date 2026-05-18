using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controller de la pantalla de "Nivel completado". Vive desactivada y se activa
/// cuando LevelFlowManager dispara OnLevelComplete. Sin logica de juego: solo
/// mostrar/ocultar y delegar el "siguiente nivel" a LevelBuilderManager.
/// </summary>
public class LevelCompleteScreen : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private LevelFlowManager levelFlow;
    [SerializeField] private LevelBuilderManager builder;

    [Header("View")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private Button continueButton;

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
        if (continueButton != null) continueButton.onClick.AddListener(HandleContinue);
    }

    void OnEnable()
    {
        if (levelFlow != null)
        {
            levelFlow.OnLevelComplete += Show;
            levelFlow.OnLevelStarted += Hide;
        }
    }

    void OnDisable()
    {
        if (levelFlow != null)
        {
            levelFlow.OnLevelComplete -= Show;
            levelFlow.OnLevelStarted -= Hide;
        }
    }

    void Show()
    {
        if (panel != null) panel.SetActive(true);
        if (titleLabel != null && builder != null)
            titleLabel.text = $"Nivel {builder.DisplayLevelNumber} completado";
    }

    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    void HandleContinue()
    {
        Hide();
        if (builder != null) builder.NextLevel();
    }
}
