/*
 *Copyright© 2025 KIUCHI/RED NIGHT WORKS
 *Released under the MIT License.
 *https://opensource.org/license/mit
*/

using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System;
using System.Reflection;

namespace RedNightWorks.Generic.MouseNavigation
{
    [InitializeOnLoad]
    public class MouseNavigation
    {
        private const bool DEBUG_MODE = false; //ログ出力モード
        private static List<string> navigationHistory = new List<string>();
        private static int historyIndex = -1;
        private static bool isNavigating = false;
        private static string lastFolderPath = null;

        static MouseNavigation()
        {
            EditorApplication.update += OnUpdate;
            SceneView.duringSceneGui += OnSceneGUI;
            if(DEBUG_MODE) Debug.Log("MouseNavigation initialized.");
        }

        private static void OnUpdate()
        {
            // フォルダ変更の検知
            string currentFolderPath = GetProjectBrowserFolderPath();
            if (!string.IsNullOrEmpty(currentFolderPath) && currentFolderPath != lastFolderPath && !isNavigating)
            {
                if(DEBUG_MODE) Debug.Log($"Folder changed: {currentFolderPath}, historyIndex: {historyIndex}");

                if (historyIndex < navigationHistory.Count - 1)
                {
                    navigationHistory.RemoveRange(historyIndex + 1, navigationHistory.Count - historyIndex - 1);
                    if(DEBUG_MODE) Debug.Log($"Cleared future history. New history count: {navigationHistory.Count}");
                }

                navigationHistory.Add(currentFolderPath);
                historyIndex++;
                lastFolderPath = currentFolderPath;

                if(DEBUG_MODE) Debug.Log($"Added to history: {currentFolderPath}, new historyIndex: {historyIndex}");
            }
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            HandleMouseEvents(Event.current);
        }

        [InitializeOnLoadMethod]
        private static void InitializeProjectWindowHook()
        {
            // プロジェクトウィンドウのイベントを捕捉
            EditorApplication.projectWindowItemOnGUI += (guid, rect) =>
            {
                Event e = Event.current;
                if (e != null)
                {
                    HandleMouseEvents(e);
                }
            };
        }

        private static void HandleMouseEvents(Event e)
        {
            if (e == null) return;

            if (e.type == EventType.MouseDown)
            {
                if (e.button == 3) // 戻るボタン (Mouse4)
                {
                    if(DEBUG_MODE) Debug.Log($"Back button pressed in {(EditorWindow.focusedWindow != null ? EditorWindow.focusedWindow.titleContent.text : "unknown")} window.");
                    NavigateBack();
                    e.Use();
                }
                else if (e.button == 4) // 進むボタン (Mouse5)
                {
                    if(DEBUG_MODE) Debug.Log($"Forward button pressed in {(EditorWindow.focusedWindow != null ? EditorWindow.focusedWindow.titleContent.text : "unknown")} window.");
                    NavigateForward();
                    e.Use();
                }
            }
        }

        private static void NavigateBack()
        {
            if (historyIndex > 0)
            {
                isNavigating = true;
                historyIndex--;
                string path = navigationHistory[historyIndex];
                if(DEBUG_MODE) Debug.Log($"Attempting to navigate back to: {path}");
                SetProjectBrowserFolder(path);
                lastFolderPath = path;
                isNavigating = false;
                if(DEBUG_MODE) Debug.Log($"Navigated back to: {path}, historyIndex: {historyIndex}");
            }
            else
            {
                if(DEBUG_MODE) Debug.Log("Cannot navigate back: at the start of history.");
            }
        }

        private static void NavigateForward()
        {
            if (historyIndex < navigationHistory.Count - 1)
            {
                isNavigating = true;
                historyIndex++;
                string path = navigationHistory[historyIndex];
                if(DEBUG_MODE) Debug.Log($"Attempting to navigate forward to: {path}");
                SetProjectBrowserFolder(path);
                lastFolderPath = path;
                isNavigating = false;
                if(DEBUG_MODE) Debug.Log($"Navigated forward to: {path}, historyIndex: {historyIndex}");
            }
            else
            {
                if(DEBUG_MODE) Debug.Log("Cannot navigate forward: at the end of history.");
            }
        }

        private static void FocusProjectWindow()
        {
            Type projectBrowserType = Type.GetType("UnityEditor.ProjectBrowser, UnityEditor");
            if (projectBrowserType != null)
            {
                EditorWindow.FocusWindowIfItsOpen(projectBrowserType);
                if(DEBUG_MODE) Debug.Log("Focused Project Window.");
            }
            else
            {
                if(DEBUG_MODE) Debug.LogWarning("ProjectBrowser type not found.");
            }
        }

        private static string GetProjectBrowserFolderPath()
        {
            try
            {
                Type projectBrowserType = Type.GetType("UnityEditor.ProjectBrowser, UnityEditor");
                if (projectBrowserType == null)
                {
                    if(DEBUG_MODE) Debug.LogWarning("ProjectBrowser type not found.");
                    return "";
                }

                EditorWindow projectBrowser = EditorWindow.GetWindow(projectBrowserType, false, null, false);
                if (projectBrowser == null)
                {
                    if(DEBUG_MODE) Debug.LogWarning("ProjectBrowser window not found.");
                    return "";
                }

                FieldInfo searchFilterField = projectBrowserType.GetField("m_SearchFilter", BindingFlags.NonPublic | BindingFlags.Instance);
                if (searchFilterField != null)
                {
                    object searchFilter = searchFilterField.GetValue(projectBrowser);
                    if (searchFilter != null)
                    {
                        PropertyInfo foldersProperty = searchFilter.GetType().GetProperty("folders", BindingFlags.Public | BindingFlags.Instance);
                        if (foldersProperty != null)
                        {
                            string[] folders = (string[])foldersProperty.GetValue(searchFilter);
                            if (folders != null && folders.Length > 0)
                            {
                                string path = folders[0];
                                if (path != lastFolderPath)
                                {
                                    if(DEBUG_MODE) Debug.Log($"Found folder from m_SearchFilter: {path}");
                                }
                                return path;
                            }
                            else
                            {
                                if(DEBUG_MODE) Debug.Log("No folders in m_SearchFilter.");
                            }
                        }
                        else
                        {
                            if(DEBUG_MODE) Debug.LogWarning("folders property not found in m_SearchFilter.");
                        }
                    }
                    else
                    {
                        if(DEBUG_MODE) Debug.Log("m_SearchFilter is null.");
                    }
                }
                else
                {
                    if(DEBUG_MODE) Debug.LogWarning("m_SearchFilter field not found.");
                }

                string selectedPath = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!string.IsNullOrEmpty(selectedPath) && System.IO.Directory.Exists(selectedPath))
                {
                    if(DEBUG_MODE) Debug.Log($"Fallback: Using selected folder: {selectedPath}");
                    return selectedPath;
                }

                return "";
            }
            catch (Exception ex)
            {
                if(DEBUG_MODE) Debug.LogError($"Error in GetProjectBrowserFolderPath: {ex.Message}");
                return "";
            }
        }

        private static void SetProjectBrowserFolder(string folderPath)
        {
            try
            {
                Type projectBrowserType = Type.GetType("UnityEditor.ProjectBrowser, UnityEditor");
                if (projectBrowserType == null)
                {
                    if(DEBUG_MODE) Debug.LogWarning("ProjectBrowser type not found.");
                    return;
                }

                EditorWindow projectBrowser = EditorWindow.GetWindow(projectBrowserType, false, null, false);
                if (projectBrowser == null)
                {
                    if(DEBUG_MODE) Debug.LogWarning("ProjectBrowser window not found.");
                    return;
                }

                // m_SearchFilter にフォルダを設定
                FieldInfo searchFilterField = projectBrowserType.GetField("m_SearchFilter", BindingFlags.NonPublic | BindingFlags.Instance);
                if (searchFilterField != null)
                {
                    object searchFilter = searchFilterField.GetValue(projectBrowser);
                    if (searchFilter != null)
                    {
                        PropertyInfo foldersProperty = searchFilter.GetType().GetProperty("folders", BindingFlags.Public | BindingFlags.Instance);
                        if (foldersProperty != null)
                        {
                            foldersProperty.SetValue(searchFilter, new[] { folderPath });
                            searchFilterField.SetValue(projectBrowser, searchFilter);
                            if(DEBUG_MODE) Debug.Log($"Set m_SearchFilter folders to: {folderPath}");
                        }
                        else
                        {
                            if(DEBUG_MODE) Debug.LogWarning("folders property not found in m_SearchFilter.");
                        }
                    }
                    else
                    {
                        if(DEBUG_MODE) Debug.LogWarning("m_SearchFilter is null.");
                    }
                }
                else
                {
                    if(DEBUG_MODE) Debug.LogWarning("m_SearchFilter field not found.");
                }

                // ShowFolderContents を使用
                int folderInstanceID = AssetDatabase.LoadMainAssetAtPath(folderPath).GetInstanceID();
                MethodInfo showFolderContentsMethod = projectBrowserType.GetMethod("ShowFolderContents", BindingFlags.NonPublic | BindingFlags.Instance);
                if (showFolderContentsMethod != null)
                {
                    showFolderContentsMethod.Invoke(projectBrowser, new object[] { folderInstanceID, true });
                    if(DEBUG_MODE) Debug.Log($"Called ShowFolderContents for: {folderPath}");
                }
                else
                {
                    if(DEBUG_MODE) Debug.LogWarning("ShowFolderContents method not found.");
                }

                FocusProjectWindow();
            }
            catch (Exception ex)
            {
                if(DEBUG_MODE) Debug.LogError($"Error in SetProjectBrowserFolder: {ex.Message}");
            }
        }
    }
}
