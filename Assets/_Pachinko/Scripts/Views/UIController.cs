using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _scoreLabel;
    [SerializeField] private TextMeshProUGUI _chipsLabel;
    [SerializeField] private TextMeshProUGUI _roundResultLabel;
    [SerializeField] private Button          _dropButton;

    public event Action OnDropPressed;

    private void Awake()
    {
        _dropButton.onClick.AddListener(() => OnDropPressed?.Invoke());
        _roundResultLabel.gameObject.SetActive(false);
    }

    public void UpdateHUD(int score, int chipsLeft, bool dropEnabled)
    {
        _scoreLabel.text         = $"Score: {score:N0}";
        _chipsLabel.text         = $"Chips: {chipsLeft}";
        _dropButton.interactable = dropEnabled;
    }

    public void ShowRoundResult(int total)
    {
        _roundResultLabel.text = $"Round over!  Total: {total:N0}";
        _roundResultLabel.gameObject.SetActive(true);
        _dropButton.interactable = false;
    }

    public void HideRoundResult() => _roundResultLabel.gameObject.SetActive(false);
}
