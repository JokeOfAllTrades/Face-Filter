using UnityEngine;

namespace Evereal.VideoCapture.Demo
{
  public class VideoCaptureProUI : MonoBehaviour
  {
    private void Awake()
    {
      Application.runInBackground = true;
    }

    private void OnGUI()
    {
      if (VideoCaptureProCtrl.instance.status == VideoCaptureProCtrl.StatusType.NOT_START)
      {
        if (GUI.Button(new Rect(10, Screen.height - 60, 150, 50), "Start Capture"))
        {
          VideoCaptureProCtrl.instance.StartCapture();
        }
      }
      else if (VideoCaptureProCtrl.instance.status == VideoCaptureProCtrl.StatusType.STARTED)
      {
        if (GUI.Button(new Rect(10, Screen.height - 60, 150, 50), "Stop Capture"))
        {
          VideoCaptureProCtrl.instance.StopCapture();
        }
        if (GUI.Button(new Rect(180, Screen.height - 60, 150, 50), "Pause Capture"))
        {
          VideoCaptureProCtrl.instance.ToggleCapture();
        }
      }
      else if (VideoCaptureProCtrl.instance.status == VideoCaptureProCtrl.StatusType.PAUSED)
      {
        if (GUI.Button(new Rect(10, Screen.height - 60, 150, 50), "Stop Capture"))
        {
          VideoCaptureProCtrl.instance.StopCapture();
        }
        if (GUI.Button(new Rect(180, Screen.height - 60, 150, 50), "Continue Capture"))
        {
          VideoCaptureProCtrl.instance.ToggleCapture();
        }
      }
      else if (VideoCaptureProCtrl.instance.status == VideoCaptureProCtrl.StatusType.STOPPED)
      {
        if (GUI.Button(new Rect(10, Screen.height - 60, 150, 50), "Processing"))
        {
          // Waiting processing end.
        }
      }
      else if (VideoCaptureProCtrl.instance.status == VideoCaptureProCtrl.StatusType.FINISH)
      {
        if (GUI.Button(new Rect(10, Screen.height - 60, 150, 50), "Open Video Folder"))
        {
          // Open video save directory.
          FunctionUtils.OpenSaveFolder();
        }
      }
    }
  }
}