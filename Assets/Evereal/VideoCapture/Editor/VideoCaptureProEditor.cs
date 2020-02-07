using UnityEngine;
using UnityEditor;
//using UnityEngine.SceneManagement;
//using UnityEditor.SceneManagement;

namespace Evereal.VideoCapture.Editor
{
  /// <summary>
  /// <c>VideoCapturePro</c> component editor.
  /// </summary>
  [CustomEditor(typeof(VideoCapturePro))]
  public class VideoCaptureProEditor : UnityEditor.Editor
  {
    public override void OnInspectorGUI()
    {
      VideoCapturePro videoCapture = (VideoCapturePro)target;
      GUILayout.BeginVertical("box");
      GUILayout.Label("Capture Mode");
      videoCapture.mode = (VideoCapturePro.ModeType)EditorGUILayout.EnumPopup("Mode", videoCapture.mode);

      //if (videoCapture.mode == VideoCapturePro.ModeType.LIVE_STREAMING)
      //{
      //  videoCapture.streamingAddress = EditorGUILayout.TextField("Streaming Server Address", videoCapture.streamingAddress);
      //}
      //else
      //{
      videoCapture.customPath = EditorGUILayout.Toggle(new GUIContent("Use Custom Path", "Use external folder Path"), videoCapture.customPath);
      if (videoCapture.customPath)
      {
        videoCapture.customPathFolder = EditorGUILayout.TextField("Custom Path Folder", videoCapture.customPathFolder);
        PathConfig.SaveFolder = videoCapture.customPathFolder + @"\";
      }
      else
      {
        PathConfig.SaveFolder = "";
      }
      GUILayout.Label(PathConfig.SaveFolder);
      //}
      GUILayout.EndVertical();

      GUILayout.BeginVertical("box");
      GUILayout.Label("Capture Format");
      videoCapture.format = (VideoCapturePro.FormatType)EditorGUILayout.EnumPopup("Format", videoCapture.format);
      if (videoCapture.format == VideoCapturePro.FormatType.NORMAL)
      {
        if (videoCapture.isDedicated)
        {
          videoCapture.frameSize = (VideoCapturePro.FrameSizeType)EditorGUILayout.EnumPopup("Frame Size", videoCapture.frameSize);
        }
      }
      else if (videoCapture.format == VideoCapturePro.FormatType.PANORAMA)
      {
        videoCapture.sceneCamera = (Camera)EditorGUILayout.ObjectField("Capture Camera", videoCapture.sceneCamera, typeof(Camera), true);
        GUILayout.BeginVertical("box");
        GUILayout.Label("Projection Format");
        videoCapture.panoramaProjection = (VideoCapturePro.PanoramaProjectionType)EditorGUILayout.EnumPopup("Projection Type", videoCapture.panoramaProjection);
        if (videoCapture.panoramaProjection == VideoCapturePro.PanoramaProjectionType.EQUIRECTANGULAR)
        {
          videoCapture.frameSize = (VideoCapturePro.FrameSizeType)EditorGUILayout.EnumPopup("Frame Size", videoCapture.frameSize);
          videoCapture.sphereOffset = EditorGUILayout.Vector2Field("Offset Spherical Coordinates", videoCapture.sphereOffset);
          videoCapture.sphereScale = EditorGUILayout.Vector2Field("Offset Spherical Coordinates", videoCapture.sphereScale);
        }
        videoCapture._cubemapSize = (VideoCapturePro.CubemapSizeType)EditorGUILayout.EnumPopup("Cubemap Size", videoCapture._cubemapSize);
        videoCapture.captureGUI = EditorGUILayout.Toggle("Capture GUI", videoCapture.captureGUI);
        GUILayout.EndVertical();
      }
      videoCapture.stereo = (VideoCapturePro.StereoType)EditorGUILayout.EnumPopup("Stereo Type", videoCapture.stereo);
      if (videoCapture.stereo != VideoCapturePro.StereoType.NONE)
      {
        GUILayout.BeginVertical("box");
        GUILayout.Label("Stereo Format");
        videoCapture.stereoFormat = (VideoCapturePro.StereoFormatType)EditorGUILayout.EnumPopup("Stereo Format Type", videoCapture.stereoFormat);
        videoCapture.interPupillaryDistance = EditorGUILayout.FloatField("Inter Pupillary Distance", videoCapture.interPupillaryDistance);
        GUILayout.EndVertical();
      }
      videoCapture._antiAliasing = (VideoCapturePro.AntiAliasingType)EditorGUILayout.EnumPopup("Anti Aliasing", videoCapture._antiAliasing);
      videoCapture._targetFramerate = (VideoCapturePro.TargetFramerateType)EditorGUILayout.EnumPopup("Target FrameRate", videoCapture._targetFramerate);
      videoCapture.isDedicated = EditorGUILayout.Toggle("Dedicated Camera", videoCapture.isDedicated);
      videoCapture.captureAudioInGPUEncoder = EditorGUILayout.Toggle("Audio Capture by Hardware", videoCapture.captureAudioInGPUEncoder);
      if (!videoCapture.captureAudioInGPUEncoder)
      {
        videoCapture.audioCapture = (AudioCapture)EditorGUILayout.ObjectField("Audio Capture", videoCapture.audioCapture, typeof(AudioCapture), true);
      }
      if (GUILayout.Button("Re-Encode Video Resolution to 4K"))
      {
        FunctionUtils.EncodeVideo4K(PathConfig.lastVideoFile);
      }
      if (GUILayout.Button("Generate GIF Image"))
      {
        FunctionUtils.ConvertVideoGif(PathConfig.lastVideoFile);
      }
      if (GUILayout.Button("Open Save folder"))
      {
        FunctionUtils.OpenSaveFolder();
      }
      GUILayout.EndVertical();
      if (GUI.changed)
      {
        EditorUtility.SetDirty(target);
//#if UNITY_5_4_OR_NEWER
//        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
//#endif
      }

    }
  }
}