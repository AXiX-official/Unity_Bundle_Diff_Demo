using System;
using System.IO;
using System.Threading.Tasks;
using UnityAsset.NET;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public class BundleDiffWindow : EditorWindow
    {
        private string _oldBundleDir = "";
        private string _newBundleDir = "";
        private string _outputDir = "";
        private string _version = "1.0.0";
        private string _baseVersion = "0.0.0";

        private bool _isGenerating;
        private string _statusMessage = "";
        private float _progress;

        [MenuItem("Tools/Bundle Diff Patch/Generate Patch", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<BundleDiffWindow>("Bundle Diff Patch");
            window.minSize = new Vector2(450, 320);
            window.Show();
        }

        private void OnEnable()
        {
            // 默认使用项目目录下的路径
            var projectPath = Directory.GetParent(Application.dataPath).FullName;
            _oldBundleDir = Path.Combine(projectPath, "testdata", "v1");
            _newBundleDir = Path.Combine(projectPath, "testdata", "v2");
            _outputDir = Path.Combine(projectPath, "testdata", "diff");
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(10);

            EditorGUILayout.LabelField("Bundle Diff Patch Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space(10);

            // Old Bundle Directory
            EditorGUILayout.LabelField("Old Bundle Directory (Base Version)", EditorStyles.label);
            EditorGUILayout.BeginHorizontal();
            {
                _oldBundleDir = EditorGUILayout.TextField(_oldBundleDir);
                if (GUILayout.Button("Browse", GUILayout.Width(70)))
                {
                    var path = EditorUtility.OpenFolderPanel("Select Old Bundle Directory", _oldBundleDir, "");
                    if (!string.IsNullOrEmpty(path))
                        _oldBundleDir = path;
            }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // New Bundle Directory
            EditorGUILayout.LabelField("New Bundle Directory (Target Version)", EditorStyles.label);
            EditorGUILayout.BeginHorizontal();
            {
                _newBundleDir = EditorGUILayout.TextField(_newBundleDir);
                if (GUILayout.Button("Browse", GUILayout.Width(70)))
                {
                    var path = EditorUtility.OpenFolderPanel("Select New Bundle Directory", _newBundleDir, "");
                    if (!string.IsNullOrEmpty(path))
                        _newBundleDir = path;
            }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // Output Directory
            EditorGUILayout.LabelField("Output Directory", EditorStyles.label);
            EditorGUILayout.BeginHorizontal();
            {
                _outputDir = EditorGUILayout.TextField(_outputDir);
                if (GUILayout.Button("Browse", GUILayout.Width(70)))
                {
                    var path = EditorUtility.OpenFolderPanel("Select Output Directory", _outputDir, "");
                    if (!string.IsNullOrEmpty(path))
                        _outputDir = path;
            }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);

            // Version Settings
            EditorGUILayout.LabelField("Version Settings", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("New Version:", GUILayout.Width(100));
                _version = EditorGUILayout.TextField(_version);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("Base Version:", GUILayout.Width(100));
                _baseVersion = EditorGUILayout.TextField(_baseVersion);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(15);

            // Status
            if (!string.IsNullOrEmpty(_statusMessage))
            {
                if (_isGenerating)
                {
                    EditorGUILayout.HelpBox(_statusMessage, MessageType.Info);
                    EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.Height(20)), _progress, $"Progress: {(_progress * 100):F0}%");
                }
                else
                {
                    EditorGUILayout.HelpBox(_statusMessage, MessageType.Info);
                }
            }

            EditorGUILayout.Space(10);

            // Buttons
            EditorGUI.BeginDisabledGroup(_isGenerating);
            {
                EditorGUILayout.BeginHorizontal();
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Generate Patch", GUILayout.Width(150), GUILayout.Height(30)))
                    {
                        GeneratePatch();
                    }
                    GUILayout.FlexibleSpace();
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space(10);

            // Info
            EditorGUILayout.HelpBox(
                "This tool generates incremental patches between two bundle versions.\n\n" +
                "Supported operations:\n" +
                "- Add/Modify/Delete files within bundles\n" +
                "- Add/Modify/Delete entire bundles\n" +
                "- Add/Modify/Delete raw files\n\n" +
                "Make sure hdiffz.dll is placed in the project root or Plugins folder.",
                MessageType.Info);
        }

        private async void GeneratePatch()
        {
            // Validation
            if (string.IsNullOrEmpty(_oldBundleDir) || !Directory.Exists(_oldBundleDir))
            {
                EditorUtility.DisplayDialog("Error", "Old bundle directory does not exist.", "OK");
                return;
            }

            if (string.IsNullOrEmpty(_newBundleDir) || !Directory.Exists(_newBundleDir))
            {
                EditorUtility.DisplayDialog("Error", "New bundle directory does not exist.", "OK");
                return;
            }

            if (string.IsNullOrEmpty(_outputDir))
            {
                EditorUtility.DisplayDialog("Error", "Output directory is not specified.", "OK");
                return;
            }

            _isGenerating = true;
            _statusMessage = "Starting patch generation...";
            var progress = new ThrottledProgress(
                new Progress<LoadProgress>(p =>
                {
                    _statusMessage = p.StatusText;
                    _progress = (float)p.Percentage / 100f;
                    Repaint();
                }),
                TimeSpan.FromMilliseconds(100));

            try
            {
                Directory.CreateDirectory(_outputDir);

                var generator = new DiffGenerator(_oldBundleDir, _newBundleDir, _outputDir);
                
                var manifest = await Task.Run(() => generator.GenerateAsync(_version, _baseVersion, progress))
                    .ConfigureAwait(true);

                _statusMessage = $"Patch generated successfully!\n" +
                                 $"Output: {_outputDir}\n" +
                                 $"Operations: {manifest.Operations.Count}";
                _progress = 1f;
                _isGenerating = false;
                Repaint();

                EditorUtility.DisplayDialog("Success",
                    $"Patch generated successfully!\n\n" +
                    $"Version: {manifest.Version}\n" +
                    $"Base Version: {manifest.BaseVersion}\n" +
                    $"Operations: {manifest.Operations.Count}\n\n" +
                    $"Output saved to: {_outputDir}",
                    "OK");

                // Open output folder
                EditorUtility.RevealInFinder(_outputDir);
            }
            catch (Exception ex)
            {
                _isGenerating = false;
                _statusMessage = $"Error: {ex.Message}";
                _progress = 0f;
                Repaint();

                EditorUtility.DisplayDialog("Error", $"Failed to generate patch:\n\n{ex.Message}", "OK");
                Debug.LogException(ex);
            }
        }
    }
}
