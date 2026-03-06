using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using BundleDiffPatch.Runtime;
using UnityAsset.NET;
using UnityEngine;
using UnityEngine.UI;

public class PatchDemoUI : MonoBehaviour
{
    [Header("UI Components")]
    [SerializeField] private Button _applyButton;
    [SerializeField] private Slider _progressSlider;
    [SerializeField] private Text _statusText;
    [SerializeField] private Text _timeText;
    [SerializeField] private Text _progressPercentText;

    [Header("Paths")]
    [SerializeField] private string _baseDir;
    [SerializeField] private string _patchDir;
    [SerializeField] private string _outputDir;

    private bool _isApplying;
    private Stopwatch _stopwatch;

    private void Awake()
    {
        _stopwatch = new Stopwatch();

        if (_applyButton != null)
        {
            _applyButton.onClick.AddListener(OnApplyButtonClicked);
        }

        ResetUI();
    }

    private void OnDestroy()
    {
        if (_applyButton != null)
        {
            _applyButton.onClick.RemoveListener(OnApplyButtonClicked);
        }
    }

    private void Update()
    {
        if (_isApplying && _stopwatch.IsRunning && _timeText != null)
        {
            _timeText.text = _stopwatch.Elapsed.ToString(@"hh\:mm\:ss");
        }
    }

    private void ResetUI()
    {
        if (_progressSlider != null) _progressSlider.value = 0f;
        if (_statusText != null) _statusText.text = "Ready";
        if (_timeText != null) _timeText.text = "00:00:00";
        if (_progressPercentText != null) _progressPercentText.text = "0%";
        if (_applyButton != null) _applyButton.interactable = true;
    }

    public void OnApplyButtonClicked()
    {
        if (_isApplying) return;
        _ = ApplyPatchAsync();
    }

    public void SetPaths(string baseDir, string patchDir, string outputDir)
    {
        _baseDir = baseDir;
        _patchDir = patchDir;
        _outputDir = outputDir;
    }

    private async Task ApplyPatchAsync()
    {
        var manifestPath = Path.Combine(_patchDir, "manifest.json");

        // Validation
        if (!Directory.Exists(_baseDir))
        {
            UpdateUI(0f, $"Base dir not found: {_baseDir}");
            return;
        }

        if (!Directory.Exists(_patchDir))
        {
            UpdateUI(0f, $"Patch dir not found: {_patchDir}");
            return;
        }

        if (!File.Exists(manifestPath))
        {
            UpdateUI(0f, $"manifest.json not found");
            return;
        }

        _isApplying = true;
        _stopwatch.Restart();

        if (_applyButton != null) _applyButton.interactable = false;

        UpdateUI(0f, "Applying patch...");

        try
        {
            Directory.CreateDirectory(_outputDir);

            var applier = new PatchApplier(_baseDir, _patchDir, _outputDir);
            await applier.ApplyAsync(manifestPath, new Progress<LoadProgress>(OnProgress));

            _stopwatch.Stop();
            UpdateUI(1f, "Patch completed!");
            UnityEngine.Debug.Log($"Patch applied successfully! Time: {_stopwatch.Elapsed}");
        }
        catch (Exception ex)
        {
            _stopwatch.Stop();
            UpdateUI(0f, $"Error: {ex.Message}");
            UnityEngine.Debug.LogException(ex);
        }
        finally
        {
            _isApplying = false;
            if (_applyButton != null) _applyButton.interactable = true;
        }
    }

    private void OnProgress(LoadProgress progress)
    {
        UpdateUI((float)progress.Percentage / 100f, progress.StatusText);
    }

    private void UpdateUI(float progress, string status)
    {
        if (_progressSlider != null) _progressSlider.value = progress;
        if (_statusText != null) _statusText.text = status;
        if (_progressPercentText != null) _progressPercentText.text = $"{(progress * 100):F1}%";
    }
}
