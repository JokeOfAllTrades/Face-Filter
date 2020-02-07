using System.IO;
using System.Diagnostics;

namespace Evereal.VideoCapture
{
  public class FunctionUtils
  {
    public static void EncodeVideo4K(string videoFile)
    {
      if (videoFile.Length == 0)
        return;
      string ext = Path.GetExtension(videoFile);
      Process.Start(PathConfig.ffmpegPath, " -i " + videoFile + " -s 3840x2160  " + videoFile.Replace(ext, "_4K" + ext));
    }

    public static void ConvertVideoGif(string videoFile)
    {
      if (videoFile.Length == 0)
        return;
      string ext = Path.GetExtension(videoFile);
      Process.Start(PathConfig.ffmpegPath, " -i " + PathConfig.lastVideoFile + " -s 1920x1080 -pix_fmt rgb24  " + videoFile.Replace(ext, ".gif"));
    }

    public static void OpenSaveFolder()
    {
      Process.Start(PathConfig.saveFolder);
      // Process.Start(new ProcessStartInfo()
      // {
      //   FileName = PathConfig.SaveFolder,
      //   UseShellExecute = true,
      //   Verb = "open"
      // });
    }
  }
}