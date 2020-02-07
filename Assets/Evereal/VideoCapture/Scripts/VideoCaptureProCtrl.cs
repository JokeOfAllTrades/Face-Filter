using UnityEngine;
using System.Collections;
using System.IO;
using System.Collections.Generic;

namespace Evereal.VideoCapture
{
  /// <summary>
  /// <c>VideoCaptureProCtrl</c> component, manage and record gameplay from specific camera.
  /// Work with <c>VideoCapturePro</c> component to generate gameplay videos.
  /// </summary>
  public class VideoCaptureProCtrl : VideoCaptureCtrlBase
  {
    /// <summary>
    /// Initial instance and init variable.
    /// </summary>
    protected override void Awake()
    {
      base.Awake();
      // For easy access the CameraCaptures var.
      if (videoCaptures == null)
        videoCaptures = new VideoCapturePro[0];
      // Create default root folder if not created.
      if (!Directory.Exists(PathConfig.SaveFolder))
      {
        Directory.CreateDirectory(PathConfig.SaveFolder);
      }
      status = StatusType.NOT_START;
    }

    /// <summary>
    /// Initialize the attributes of the capture session and start capture.
    /// </summary>
    public override void StartCapture()
    {
      if (status != StatusType.NOT_START &&
          status != StatusType.FINISH)
      {
        Debug.LogWarning("[VideoCaptureProCtrl::StartCapture] Previous capture not finish yet!");
        return;
      }
      // Filter out disabled capture component.
      List<VideoCapturePro> validCaptures = new List<VideoCapturePro>();
      if (validCaptures != null && videoCaptures.Length > 0)
      {
        foreach (VideoCapturePro videoCapture in videoCaptures)
        {
          if (videoCapture != null && videoCapture.gameObject.activeSelf)
          {
            validCaptures.Add(videoCapture);
          }
        }
      }
      videoCaptures = validCaptures.ToArray();
      for (int i = 0; i < videoCaptures.Length; i++)
      {
        VideoCapturePro videoCapture = (VideoCapturePro)videoCaptures[i];
        if (videoCapture == null || !videoCapture.gameObject.activeSelf)
        {
          continue;
        }
        videoCapture.StartCapture();
      }
      status = StatusType.STARTED;
    }

    /// <summary>
    /// Stop video capture process and check FINISH status.
    /// </summary>
    public override void StopCapture()
    {
      if (status != StatusType.STARTED && status != StatusType.PAUSED)
      {
        Debug.LogWarning("[VideoCaptureProCtrl::StopCapture] capture session not start yet!");
        return;
      }
      foreach (VideoCapturePro videoCapture in videoCaptures)
      {
        if (!videoCapture.gameObject.activeSelf)
        {
          continue;
        }
        videoCapture.StopCapture();
        PathConfig.lastVideoFile = videoCapture.filePath;
      }
      status = StatusType.STOPPED;
      StartCoroutine(CheckCapturingFinish());
    }
    /// <summary>
    /// Pause video capture process.
    /// </summary>
    public override void ToggleCapture()
    {
      foreach (VideoCapturePro videoCapture in videoCaptures)
      {
        if (!videoCapture.gameObject.activeSelf)
        {
          continue;
        }
        videoCapture.ToggleCapture();
      }
      if (status != StatusType.PAUSED)
      {
        status = StatusType.PAUSED;
      }
      else
      {
        status = StatusType.STARTED;
      }
    }
    private IEnumerator CheckCapturingFinish()
    {
      while (true)
      {
        // At least wait 1 second.
        yield return new WaitForSeconds(1);
        bool capturing = false;
        foreach (VideoCapturePro videoCapture in videoCaptures)
        {
          if (!videoCapture.gameObject.activeSelf)
          {
            continue;
          }
          if (videoCapture.capturingStart)
          {
            capturing = true;
            break;
          }
        }
        if (!capturing)
        {
          status = StatusType.FINISH;
          foreach (VideoCapturePro videoCapture in videoCaptures)
          {
            videoCapture.Cleanup();
          }
          break;
        }
      }
    }
  }
}