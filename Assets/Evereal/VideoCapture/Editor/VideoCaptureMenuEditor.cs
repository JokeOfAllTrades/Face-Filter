using System.IO;
using UnityEngine;
using UnityEditor;

namespace Evereal.VideoCapture.Editor
{
  public class VideoCaptureMenuEditor : MonoBehaviour
  {
    [MenuItem("Tools/Evereal/VideoCapture/Download FFmpeg/Windows Build", false, 1)]
    private static void DownloadFFmpegForWindows()
    {
      if (!Directory.Exists(PathConfig.windowsFFmpegFolderPath))
      {
        Directory.CreateDirectory(PathConfig.windowsFFmpegFolderPath);
      }
      CmdProcess.Run("curl", PathConfig.windowsFFmpegDownloadUrl + " --output " + "\"" + PathConfig.windowsFFmpegPath + "\"");
      UnityEngine.Debug.Log("Download Windows FFmpeg done!");
    }

    [MenuItem("Tools/Evereal/VideoCapture/Download FFmpeg/macOS Build", false, 1)]
    private static void DownloadFFmpegForOSX()
    {
      if (!Directory.Exists(PathConfig.macOSFFmpegFolderPath))
      {
        Directory.CreateDirectory(PathConfig.macOSFFmpegFolderPath);
      }
      CmdProcess.Run("curl", PathConfig.macOSFFmpegDownloadUrl + " --output " + "\"" + PathConfig.macOSFFmpegPath + "\"");
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
      CmdProcess.Run("chmod", "a+x " + "\"" + PathConfig.macOSFFmpegPath + "\"");
      UnityEngine.Debug.Log("Grant permission for: " + PathConfig.macOSFFmpegPath);
#endif
      UnityEngine.Debug.Log("Download macOS FFmpeg done!");
    }

    [MenuItem("Tools/Evereal/VideoCapture/Grant FFmpeg Permission/macOS Build", false, 5)]
    private static void GrantFFmpegPermissionForOSX()
    {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
      CmdProcess.Run("chmod", "a+x " + PathConfig.macOSFFmpegPath);
      UnityEngine.Debug.Log("Grant permission for: " + PathConfig.macOSFFmpegPath);
#endif
    }

    [MenuItem("Tools/Evereal/VideoCapture/Create GameObject/Software Encoder/DedicatedCapture", false, 10)]
    private static void CreateDedicatedCaptureObject(MenuCommand menuCommand)
    {
      GameObject videoCapturePrefab = PrefabUtility.InstantiatePrefab(Resources.Load("Prefabs/DedicatedCapture")) as GameObject;
      videoCapturePrefab.name = "DedicatedCapture";
      PrefabUtility.DisconnectPrefabInstance(videoCapturePrefab);
      GameObjectUtility.SetParentAndAlign(videoCapturePrefab, menuCommand.context as GameObject);
      Undo.RegisterCreatedObjectUndo(videoCapturePrefab, "Create " + videoCapturePrefab.name);
      Selection.activeObject = videoCapturePrefab;
      InitCaptureProperty();
    }

    [MenuItem("Tools/Evereal/VideoCapture/Create GameObject/Software Encoder/360Capture", false, 10)]
    private static void Create360CaptureObject(MenuCommand menuCommand)
    {
      GameObject videoCapturePrefab = PrefabUtility.InstantiatePrefab(Resources.Load("Prefabs/360Capture")) as GameObject;
      videoCapturePrefab.name = "360Capture";
      PrefabUtility.DisconnectPrefabInstance(videoCapturePrefab);
      GameObjectUtility.SetParentAndAlign(videoCapturePrefab, menuCommand.context as GameObject);
      Undo.RegisterCreatedObjectUndo(videoCapturePrefab, "Create " + videoCapturePrefab.name);
      Selection.activeObject = videoCapturePrefab;
      InitCaptureProperty();
    }
    [MenuItem("Tools/Evereal/VideoCapture/Create GameObject/Software Encoder/MainCapture", false, 10)]
    private static void CreateMainCaptureObject(MenuCommand menuCommand)
    {
      Camera[] cameras = FindObjectsOfType(typeof(Camera)) as Camera[];
      if (cameras.Length >= 0)
      {
        foreach (var cameraItem in cameras)
        {
          if (cameraItem == Camera.main)
          {
            DestroyImmediate(cameraItem.gameObject);
          }
        }
      }
      GameObject videoCapturePrefab = PrefabUtility.InstantiatePrefab(Resources.Load("Prefabs/MainCapture")) as GameObject;
      videoCapturePrefab.name = "MainCapture";
      PrefabUtility.DisconnectPrefabInstance(videoCapturePrefab);
      GameObjectUtility.SetParentAndAlign(videoCapturePrefab, menuCommand.context as GameObject);
      Undo.RegisterCreatedObjectUndo(videoCapturePrefab, "Create " + videoCapturePrefab.name);
      Selection.activeObject = videoCapturePrefab;
      InitCaptureProperty();
    }

    private static void InitCaptureProperty()
    {
      VideoCapture[] videoCaptures = FindObjectsOfType(typeof(VideoCapture)) as VideoCapture[];
      VideoCaptureCtrl videoCaptureCtrl = FindObjectOfType(typeof(VideoCaptureCtrl)) as VideoCaptureCtrl;
      if (videoCaptureCtrl == null || videoCaptures.Length <= 0)
      {
        return;
      }
      videoCaptureCtrl.videoCaptures = new VideoCapture[videoCaptures.Length];
      for (int i = 0; i < videoCaptures.Length; i++)
      {
        videoCaptureCtrl.videoCaptures[i] = videoCaptures[i];
      }
    }

#if VIDEO_CAPTURE_PRO

    [MenuItem("Tools/Evereal/VideoCapture/Create GameObject/GPU Encoder/DedicatedCapturePro", false, 10)]
    private static void CreateDedicatedCaptureProObject(MenuCommand menuCommand)
    {
      GameObject videoCapturePrefab = PrefabUtility.InstantiatePrefab(Resources.Load("Prefabs/DedicatedCapturePro")) as GameObject;
      videoCapturePrefab.name = "DedicatedCapturePro";
      PrefabUtility.DisconnectPrefabInstance(videoCapturePrefab);
      GameObjectUtility.SetParentAndAlign(videoCapturePrefab, menuCommand.context as GameObject);
      Undo.RegisterCreatedObjectUndo(videoCapturePrefab, "Create " + videoCapturePrefab.name);
      Selection.activeObject = videoCapturePrefab;
      InitProCaptureProperty();
    }

    [MenuItem("Tools/Evereal/VideoCapture/Create GameObject/GPU Encoder/360CapturePro", false, 10)]
    private static void Create360CaptureProObject(MenuCommand menuCommand)
    {
      GameObject videoCapturePrefab = PrefabUtility.InstantiatePrefab(Resources.Load("Prefabs/360CapturePro")) as GameObject;
      videoCapturePrefab.name = "360CapturePro";
      PrefabUtility.DisconnectPrefabInstance(videoCapturePrefab);
      GameObjectUtility.SetParentAndAlign(videoCapturePrefab, menuCommand.context as GameObject);
      Undo.RegisterCreatedObjectUndo(videoCapturePrefab, "Create " + videoCapturePrefab.name);
      Selection.activeObject = videoCapturePrefab;
      InitProCaptureProperty();
    }

    private static void InitProCaptureProperty()
    {
      VideoCapturePro[] videoCaptures = FindObjectsOfType(typeof(VideoCapturePro)) as VideoCapturePro[];
      VideoCaptureProCtrl videoCaptureCtrl = FindObjectOfType(typeof(VideoCaptureProCtrl)) as VideoCaptureProCtrl;
      if (videoCaptureCtrl == null || videoCaptures.Length <= 0)
      {
        return;
      }
      videoCaptureCtrl.videoCaptures = new VideoCapturePro[videoCaptures.Length];
      for (int i = 0; i < videoCaptures.Length; i++)
      {
        videoCaptureCtrl.videoCaptures[i] = videoCaptures[i];
      }
    }

    [MenuItem("Tools/Evereal/VideoCapture/Create GameObject/GPU Encoder/MainCapturePro", false, 10)]
    private static void CreateMainCaptureProObject(MenuCommand menuCommand)
    {
      Camera[] cameras = FindObjectsOfType(typeof(Camera)) as Camera[];
      if (cameras.Length >= 0)
      {
        foreach (var cameraItem in cameras)
        {
          if (cameraItem == Camera.main)
          {
            DestroyImmediate(cameraItem.gameObject);
          }
        }
      }
      GameObject videoCapturePrefab = PrefabUtility.InstantiatePrefab(Resources.Load("Prefabs/MainCapturePro")) as GameObject;
      videoCapturePrefab.name = "MainCapturePro";
      PrefabUtility.DisconnectPrefabInstance(videoCapturePrefab);
      GameObjectUtility.SetParentAndAlign(videoCapturePrefab, menuCommand.context as GameObject);
      Undo.RegisterCreatedObjectUndo(videoCapturePrefab, "Create " + videoCapturePrefab.name);
      Selection.activeObject = videoCapturePrefab;
      InitCaptureProperty();
    }
#endif

    [MenuItem("Tools/Evereal/VideoCapture/Change ColorSpace/Gamma", false, 20)]
    private static void PreparePanoramaCapture()
    {
      // Change to gamma color space.
      // https://docs.unity3d.com/Manual/LinearLighting.html
      PlayerSettings.colorSpace = ColorSpace.Gamma;
      UnityEngine.Debug.Log("Set color space to Gamma");
    }

    [MenuItem("Tools/Evereal/VideoCapture/Change ColorSpace/Linear", false, 20)]
    private static void PrepareNormalCapture()
    {
      PlayerSettings.colorSpace = ColorSpace.Linear;
      UnityEngine.Debug.Log("Set color space to Linear");
    }
  }
}