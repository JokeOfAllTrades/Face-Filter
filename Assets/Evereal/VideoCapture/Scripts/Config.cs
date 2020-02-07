using UnityEngine;
using System;

namespace Evereal.VideoCapture
{
  /// <summary>
  /// Config setup for video related path.
  /// </summary>
  public class PathConfig
  {
    public static string windowsFFmpegDownloadUrl = "https://evereal.com/download/windows/ffmpeg.exe";
    public static string macOSFFmpegDownloadUrl = "https://evereal.com/download/osx/ffmpeg";
    public static string persistentDataPath = Application.persistentDataPath;
    public static string streamingAssetsPath = Application.streamingAssetsPath;
    public static string myDocumentsPath = Environment.GetFolderPath(
        Environment.SpecialFolder.MyDocuments);
    public static string saveFolder = "";
    public static string lastVideoFile = "";
    /// <summary>
    /// The video folder, save recorded video.
    /// </summary>
    public static string SaveFolder
    {
      get
      {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        if (saveFolder == "")
        {
	  saveFolder = persistentDataPath + "/Evereal/Video/";
        }
        return SaveFolder;
#else
        if (saveFolder == "")
        {
          saveFolder = myDocumentsPath + "/Evereal/Video/";
        }
        return saveFolder;
#endif
      }
      set
      {
        saveFolder = value;
      }
    }

    /// <summary>
    /// The FFmpeg path.
    /// </summary>
    public static string windowsFFmpegFolderPath = streamingAssetsPath + "/Evereal/FFmpeg/Windows/";
    public static string macOSFFmpegFolderPath = streamingAssetsPath + "/Evereal/FFmpeg/OSX/";
    public static string windowsFFmpegPath = windowsFFmpegFolderPath + "ffmpeg.exe";
    public static string macOSFFmpegPath = macOSFFmpegFolderPath + "ffmpeg";
    public static string ffmpegPath
    {
      get
      {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        return windowsFFmpegPath;
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        return macOSFFmpegPath;
#else
        return "";
#endif
      }
    }
  }
}