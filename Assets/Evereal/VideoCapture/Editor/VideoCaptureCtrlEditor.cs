using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace Evereal.VideoCapture.Editor
{
  /// <summary>
  /// <c>VideoCaptureCtrl</c> component editor.
  /// </summary>
  [CustomEditor(typeof(VideoCaptureCtrl))]
  public class VideoCaptureCtrlEditor : UnityEditor.Editor
  {
    public override void OnInspectorGUI()
    {
      VideoCaptureCtrl videoCaptureCtrl = (VideoCaptureCtrl)target;
      GUILayout.BeginVertical("box");
      videoCaptureCtrl.debug = EditorGUILayout.Toggle("Debug Log", videoCaptureCtrl.debug);
      GUILayout.EndVertical();

      GUILayout.BeginVertical("box");
      GUILayout.Label("Capture Control");
      videoCaptureCtrl.startOnAwake = EditorGUILayout.Toggle("Start On Awake", videoCaptureCtrl.startOnAwake);
      if (videoCaptureCtrl.startOnAwake)
      {
        videoCaptureCtrl.captureTime = EditorGUILayout.FloatField("Capture Duration (Sec)", videoCaptureCtrl.captureTime);
      }
      videoCaptureCtrl.quitAfterCapture = EditorGUILayout.Toggle("Quit After Capture", videoCaptureCtrl.quitAfterCapture);
      GUILayout.EndVertical();

      GUILayout.BeginVertical("box");
      GUILayout.Label("Capture Component");
      SerializedObject serializedObject = new SerializedObject(target);
      serializedObject.Update();
      EditorGUILayout.PropertyField(serializedObject.FindProperty("_videoCaptures"), true);
      EditorGUILayout.PropertyField(serializedObject.FindProperty("_audioCapture"), false);
      serializedObject.ApplyModifiedProperties();
      GUILayout.EndVertical();

      if (GUI.changed)
      {
        EditorUtility.SetDirty(target);
#if UNITY_5_4_OR_NEWER
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
#endif
      }
    }
  }
}