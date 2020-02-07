/*
 Texture Render Workflow Graph:
 FLAT VIDEO:
                                Camera
                                  | [Camera.targetTexture]
                                  V
                            RenderTexture
                                  |
                       2D         V               STEREO
             ---------------------------------------------------
             |[RenderTexture.GetNativeTexturePtr]            |[Graphics.Blit]
             V                                               V
         Native Plugin                               StereRenderTexture
                                                             |[RenderTexture.GetNativeTexturePtr]
                                                             V
                                                        Native Plugin


 PANORAMA VIDEO:
                                      OnRenderImage
                                            |
                      CUBEMAP VIDEO         V         EQUIRECTANGULAR
               ------------------------------------------------------------
               |[Graphics Library && Graphics.Blit]                       |[Graphics.Blit]
               V                                                          V
           RenderTexture                                            RenderTexture
               |[RenderTexture.GetNativeTexturePtr]                       |
               V                                   EQUIRECTANGULAR STEREO V EQUIRECTANGULAR
         Native Plugin                                          ---------------------------------
                                                                |[Graphics.Blit]                |[RenderTexture.GetNativeTexturePtr]
                                                                V                               V
                                                        StereRenderTexture                    Native Plugin
                                                                |[RenderTexture.GetNativeTexturePtr]
                                                                V
                                                           Native Plugin

 Detailed Process:

 2D Normal Video:
     1. Init the Camera, the RenderTexture in Awake(). If the property changes, it will re-init in StartCapture().
     2. Using RenderTexture as Camera TargetTexture object to get the render data, using RenderTexture.GetNativeTexturePtr to send intptr to native plugin object.
        (Using RenderTexture as Graphics.SetRenderTarget to get the render data in OnRenderImage() when use Main Camera to capture video.)
     3. Then send the RenderTexture.GetNativeTexturePtr to the native plugin for encoding every frame.
     4. You will get the video after StopCapture() and mux process finish.
     Note: The main camera and dedicated cameras support recording of flat video and stereo video.

 Panorama Cubemap Video:
     1. Init the Camera, the RenderTexture in Awake(). If the property changes, it will re-init in StartCapture().
     2. Using RenderTexture as the Graphics.Blit object to get the render data in OnRenderImage() after StartCapture(), using Low-level graphics library to set the RenderTexture data.
     3. Then send the RenderTexture.GetNativeTexturePtr to the native plugin for encoding every frame.
     4. You will get the cubemap video after StopCapture() and mux process finish.
     Note: The main camera unsupported to capture panorama video. The Cubemap video unsupported the stereo video.

 Panorama Equirectangular Video:
     1. Init the Camera, the RenderTexture in Awake(). If the property changes, it will re-init in StartCapture().
     2. Using RenderTexture as the Graphics.Blit object to get the render data in OnRenderImage() after StartCapture().
     3. Then send the RenderTexture.GetNativeTexturePtr to the native plugin for encoding every frame.
     4. You will get the equirectangular video after StopCapture() and mux process finish.
     Note: The main camera unsupported to capture panorama video.
 */
using UnityEngine;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections;
using System.Threading;

namespace Evereal.VideoCapture
{
  /// <summary>
  /// <c>VideoCapturePro</c> component capture video with hardware accelerate.
  /// </summary>
  public class VideoCapturePro : VideoCaptureBase
  {
    /// <summary>
    /// The texture holding panorama video frame data.
    /// </summary>
    private RenderTexture outputTexture;  // equirect or cubemap ends up here
    private RenderTexture externalTexture;
    /// <summary>
    /// Panorama capture variables.
    /// </summary>
    [Tooltip("Offset spherical coordinates (shift equirect)")]
    public Vector2 sphereOffset = Vector2.zero;
    [Tooltip("Scale spherical coordinates (flip equirect, usually just 1 or -1)")]
    public Vector2 sphereScale = Vector2.one;
    [Tooltip("Reference to camera that renders the scene")]
    public Camera sceneCamera;
    /// <summary>
    /// Panorama capture materials.
    /// </summary>
    private Material convertMaterial;
    private Material outputCubemapMaterial;
    private Material downSampleMaterial;
    /// <summary>
    /// Video capture control logic.
    /// </summary>
    public bool capturingStart = false;
    public bool captureAudioInGPUEncoder = true;
    private bool capturingStop = false;
    private bool needToStopCapturing = false;
    private bool isPause = false;
    /// <summary>
    /// Keep last width, height setup.
    /// </summary>
    private int lastWidth = 0, lastHeight = 0;
    /// <summary>
    /// Event for managing thread.
    /// </summary>
    private ManualResetEvent flushThreadSig;
    private ManualResetEvent liveThreadSig;
    private ManualResetEvent threadShutdown;
    private Thread flushThread;
    private Thread audioThread;
    private Thread liveThread;

    private float fpsTimer = 0.0f;
    /// <summary>
    /// Rotate camera for cubemap lookup.
    /// </summary>
    private bool includeCameraRotation = false;
    private bool flushReady = false;
    private float flushTimer = 0.0f;
    private float flushCycle = 5.0f;
    /// <summary>
    /// The project path.
    /// </summary>
    private string projectPath;
    /// <summary>
    /// Reference to the <c>AudioCapture</c> component for writing audio files.
    /// This needs to be set when you are recording a video with audio.
    /// </summary>
    [SerializeField]
    private AudioCapture _audioCapture;
    /// <summary>
    /// Get or set the <c>AudioCapture</c> component.
    /// </summary>
    /// <value>The <c>AudioCapture</c> component.</value>
    public AudioCapture audioCapture
    {
      get
      {
        return _audioCapture;
      }
      set
      {
        _audioCapture = value;
      }
    }
    /// <summary>
    /// Start capture video.
    /// </summary>
    public override void StartCapture()
    {
      if (!isDedicated)
      {
        if (format != FormatType.NORMAL)
        {
          Debug.LogWarning(
              "[VideoCapturePro::StartCapture] The pamorama video only support dedicated camera capture!");
          return;
        }
      }
      if (!File.Exists(PathConfig.ffmpegPath))
      {
        Debug.LogError(
            "[VideoCapturePro::StartCapture] FFmpeg not found, please follow document and add ffmpeg executable before start capture!"
        );
        return;
      }
      filePath = PathConfig.SaveFolder + StringUtils.GetH264FileName(StringUtils.GetRandomString(5));
      if (isLiveStreaming)
      {
        if (!StringUtils.IsRtmpAddress(streamingAddress))
        {
          Debug.LogWarning(
             "[VideoCapturePro::StartCapture] Video live streaming require rtmp server address setup!"
          );
          return;
        }
      }
      if (frameSize == FrameSizeType._7680x4320)
      {
        // https://developer.nvidia.com/video-encode-decode-gpu-support-matrix for more details.
        Debug.LogWarning(
            "[VideoCapturePro::StartCapture] Some of graphic cards do not support 8k encoding, switch to 4k encoding. ");
        frameSize = FrameSizeType._4096x2160;
      }
      if (!capturingStart)
      {
        if (!SetOutputSize())
        {
          Debug.LogFormat("[VideoCapturePro::StartCapture] Failed due to invalid resolution: {0} x {1}", frameWidth, frameHeight);
          return;
        }
        if (isLiveStreaming)
        {
          Debug.LogFormat("[VideoCapturePro::StartCapture] Starting {0} x {1}: {2}", frameWidth, frameHeight, streamingAddress);
        }
        else
        {
          Debug.LogFormat("[VideoCapturePro::StartCapture] Starting {0} x {1}: {2}", frameWidth, frameHeight, filePath);
        }
      }
      else
      {
        Debug.LogWarning("[VideoCapturePro::StartCapture] Previous capture not finish yet!");
        return;
      }

      capturingStart = true;
      capturingStop = false;
      needToStopCapturing = false;

      flushTimer = 0.0f;
      fpsTimer = 0.0f;

      if (flushThreadSig == null)
      {
        flushThreadSig = new ManualResetEvent(true);
      }
      if (liveThreadSig == null)
      {
        liveThreadSig = new ManualResetEvent(false);
      }
      if (threadShutdown == null)
      {
        threadShutdown = new ManualResetEvent(false);
      }
      if (flushThread == null)
      {
        flushThread = new Thread(MuxingThreadFunction);
        flushThread.Start();
      }
      if (audioThread == null && captureAudioInGPUEncoder)
      {
        audioThread = new Thread(AudioThreadFunction);
        audioThread.Start();
      }
      if (isLiveStreaming && liveThread == null)
      {
        liveThread = new Thread(LiveThreadFunction);
        liveThread.Start();
      }
      if (!captureAudioInGPUEncoder)
      {
        if (audioCapture != null)
        {
          audioCapture.StartCapture();
        }
      }
    }
    /// <summary>
    /// Stop capture video.
    /// </summary>
    public override void StopCapture()
    {
      if (isPause)
      {
        isPause = false;
      }
      needToStopCapturing = true;
      if (!captureAudioInGPUEncoder)
      {
        if (audioCapture != null)
        {
          if (audioCapture.status == VideoCaptureCtrl.StatusType.STARTED && audioCapture.status == VideoCaptureCtrl.StatusType.PAUSED)
          {
            audioCapture.StopCapture();
          }
          else
          {
            return;
          }
        }
      }
    }
    /// <summary>
    /// Pause capture video.
    /// </summary>
    public override void ToggleCapture()
    {
      isPause = !isPause;
      if (!captureAudioInGPUEncoder)
      {
        if (audioCapture != null)
        {
          audioCapture.ToggleCapture();
        }
      }
    }
    #region Unity Lifecycle
    /// <summary>
    /// Called before any Start functions and also just after a prefab is instantiated.
    /// </summary>
    private new void Awake()
    {
      base.Awake();
      capturingStart = false;
      capturingStop = false;

      if (isPanorama)
      {
        // create render texture for equirectangular image
        SetOutputSize();
        cubemap2Equirectangular = Resources.Load("Materials/Cubemap2Equirectangular") as Material;
        convertMaterial = Resources.Load("Materials/Cubemap2EquirectangularPro") as Material;
        outputCubemapMaterial = Resources.Load("Materials/CubemapDisplayPro") as Material;
        downSampleMaterial = Resources.Load("Materials/DownsamplePro") as Material;
        convertMaterial.hideFlags = HideFlags.DontSave;
        outputCubemapMaterial.hideFlags = HideFlags.DontSave;
        downSampleMaterial.hideFlags = HideFlags.DontSave;

        frameRenderTexture = new RenderTexture(frameWidth, frameHeight, 24);
        captureCamera.targetTexture = frameRenderTexture;
      }
      else
      {
        // create render texture for 2d image
        SetOutputSize();
      }
    }
    /// <summary>
    /// Called once per frame.
    /// </summary>
    private void Update()
    {
      if (needToStopCapturing)
      {
        // Stop encoding
        capturingStop = true;
      }

      if (capturingStart)
      {
        flushReady = false;
      }

      flushTimer += Time.deltaTime;
      fpsTimer += Time.deltaTime;

      if (fpsTimer >= deltaFrameTime)
      {
        fpsTimer -= deltaFrameTime;
        if (capturingStart && !isPause)
        {
          if (isPanorama)
          {
            if (stereo != StereoType.NONE)
            {
              if (captureGUI)
              {
                panoramaTempRenderTexture.DiscardContents();
                RenderToCubemapWithGUI(sceneCamera, panoramaTempRenderTexture);
                // Convert to equirectangular projection.
                frameRenderTexture.DiscardContents();
                Graphics.Blit(panoramaTempRenderTexture, frameRenderTexture, cubemap2Equirectangular);
                SetStereoVideoFormat(frameRenderTexture);
                copyReverseMaterial.DisableKeyword("REVERSE_TOP_BOTTOM");
                copyReverseMaterial.EnableKeyword("REVERSE_LEFT_RIGHT");
              }
              else
              {
                sceneCamera.transform.position = transform.position;
                sceneCamera.RenderToCubemap(panoramaTempRenderTexture); // render cubemap
                Graphics.Blit(panoramaTempRenderTexture, frameRenderTexture, cubemap2Equirectangular);
                SetStereoVideoFormat(frameRenderTexture);
                copyReverseMaterial.DisableKeyword("REVERSE_TOP_BOTTOM");
                copyReverseMaterial.DisableKeyword("REVERSE_LEFT_RIGHT");
              }
              Graphics.Blit(finalTargetTexture, frameRenderTexture, copyReverseMaterial);
              GPUCaptureLib_StartEncoding(
                    frameRenderTexture.GetNativeTexturePtr(),
                    filePath,
                    isLiveStreaming,
                    targetFramerate,
                    true,
                    captureAudioInGPUEncoder);
            }
            else
            {
              if (captureGUI)
              {
                panoramaTempRenderTexture.DiscardContents();
                RenderToCubemapWithGUI(sceneCamera, panoramaTempRenderTexture);
                // Convert to equirectangular projection.
                frameRenderTexture.DiscardContents();
                Graphics.Blit(panoramaTempRenderTexture, frameRenderTexture, cubemap2Equirectangular);
                copyReverseMaterial.DisableKeyword("REVERSE_TOP_BOTTOM");
                copyReverseMaterial.EnableKeyword("REVERSE_LEFT_RIGHT");
                Graphics.Blit(frameRenderTexture, finalTargetTexture, copyReverseMaterial);
                GPUCaptureLib_StartEncoding(
                      finalTargetTexture.GetNativeTexturePtr(),
                      filePath,
                      isLiveStreaming,
                      targetFramerate,
                      true,
                      captureAudioInGPUEncoder);
              }
              else
              {
                sceneCamera.transform.position = transform.position;
                sceneCamera.RenderToCubemap(panoramaTempRenderTexture); // render cubemap
                Graphics.Blit(externalTexture, finalTargetTexture, downSampleMaterial);
                copyReverseMaterial.EnableKeyword("REVERSE_TOP_BOTTOM");
                copyReverseMaterial.DisableKeyword("REVERSE_LEFT_RIGHT");
                Graphics.Blit(finalTargetTexture, externalTexture, copyReverseMaterial);
                GPUCaptureLib_StartEncoding(
                     externalTexture.GetNativeTexturePtr(),
                     filePath,
                     isLiveStreaming,
                     targetFramerate,
                     true,
                     captureAudioInGPUEncoder);
              }
            }
          }
          else
          {
            if (stereo != StereoType.NONE)
            {
              SetStereoVideoFormat(frameRenderTexture);
              GPUCaptureLib_StartEncoding(
                  finalTargetTexture.GetNativeTexturePtr(),
                  filePath,
                  isLiveStreaming,
                  targetFramerate,
                  true,
                  captureAudioInGPUEncoder);
            }
            else
            {
              GPUCaptureLib_StartEncoding(
                  frameRenderTexture.GetNativeTexturePtr(),
                  filePath,
                  isLiveStreaming,
                  targetFramerate,
                  true,
                  captureAudioInGPUEncoder);
            }
          }
          if (flushTimer > flushCycle && isLiveStreaming)
          {
            // [Live] flush input buffers based on flush cycle value
            flushTimer = 0.0f;
            GPUCaptureLib_StopEncoding();
            flushReady = true;
          }
          else if (capturingStop && !isLiveStreaming)
          {
            // flush input buffers when got stop input
            GPUCaptureLib_StopEncoding();
            flushReady = true;
          }
        }
#if UNITY_5_6_0
        else
        {
          if (sceneCamera)
          {
            sceneCamera.transform.position = transform.position;
            sceneCamera.targetTexture = null;
          }
        }
#endif
        // Muxing
        if (flushReady && !isLiveStreaming)
        {
          // Flush inputs and Stop encoding
          flushReady = false;
          capturingStart = false;
          flushThreadSig.Set();
        }
        else if (flushReady && isLiveStreaming)
        {
          // Restart encoding after flush
          flushReady = false;
          if (capturingStop && !needToStopCapturing)
          {
            capturingStop = false;
          }
          if (capturingStart && needToStopCapturing)
          {
            capturingStart = false;
          }
          flushThreadSig.Set();
          // Signal live stream thread
          liveThreadSig.Set();
        }
      }
    }
    /// <summary>
    /// OnRenderImage is called after all rendering is complete to render image.
    /// </summary>
    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
      /*if (capturingStart && format == FormatType.NORMAL)
      {
        Graphics.Blit(src, dest);
        Graphics.SetRenderTarget(frameRenderTexture);
        Graphics.Blit(src, blitMaterial);
        Graphics.SetRenderTarget(null);
        return;
      }*/
      if (!capturingStart || !isPanorama || stereo != StereoType.NONE || isPause)
      {
        Graphics.Blit(src, dest);
        return;
      }

      if (panoramaProjection == PanoramaProjectionType.CUBEMAP)
      {
        DisplayCubeMap(dest);
      }
      else if (panoramaProjection == PanoramaProjectionType.EQUIRECTANGULAR && !captureGUI)
      {
        DisplayEquirect(dest);
      }
    }
    /// <summary>
    /// This function is called when the MonoBehaviour will be destroyed.
    /// </summary>
    private void OnDestroy()
    {
      if (isPanorama)
      {
        Destroy(panoramaTempRenderTexture);
        Destroy(outputTexture);
        Destroy(externalTexture);
      }
      if (finalTargetTexture != null)
      {
        RenderTexture.ReleaseTemporary(finalTargetTexture);
        finalTargetTexture = null;
      }
      if (frameRenderTexture != null && !isDedicated)
      {
        RenderTexture.ReleaseTemporary(frameRenderTexture);
        frameRenderTexture = null;
      }
      else if (frameRenderTexture != null && isDedicated)
      {
        RenderTexture.Destroy(frameRenderTexture);
      }
    }
    /// <summary>
    /// Sent to all game objects before the application is quit.
    /// </summary>
    private void OnApplicationQuit()
    {
      if (capturingStart)
      {
        GPUCaptureLib_StopEncoding();
        flushThreadSig.Set();
        threadShutdown.Set();
      }

      if (flushThread != null)
      {
        flushThread.Abort();
      }

      if (audioThread != null)
      {
        audioThread.Abort();
      }

      if (liveThread != null)
      {
        liveThread.Abort();
      }
    }
    #endregion

    #region Video Capture Core
    private void LiveThreadFunction()
    {
      while (true)
      {
        liveThreadSig.WaitOne(Timeout.Infinite);
        GPUCaptureLib_StartLiveStream(streamingAddress, PathConfig.ffmpegPath, targetFramerate);
        liveThreadSig.Reset();
        if (needToStopCapturing)
        {
          GPUCaptureLib_StopLiveStream();
        }
      }
    }

    private void MuxingThreadFunction()
    {
      projectPath = Environment.CurrentDirectory;
      while (true)
      {
        flushThreadSig.WaitOne(Timeout.Infinite);
        GPUCaptureLib_MuxingData(PathConfig.ffmpegPath, filePath, audioCapture ? audioCapture.filePath : "", projectPath);
        flushThreadSig.Reset();
      }
    }

    private void AudioThreadFunction()
    {
      while (true)
      {
        if (needToStopCapturing)
        {
          flushThreadSig.Reset();
        }
        else if (capturingStart && !needToStopCapturing)
        {
          GPUCaptureLib_AudioEncoding();
        }
        Thread.Sleep(10);
      }
    }

    private bool SetOutputSize()
    {
      if (frameWidth == 0 || frameHeight == 0)
      {
        Debug.LogWarning("[VideoCapturePro::SetOutputSize] The width and height shouldn't be zero.");
        return false;
      }
      if (!isPanorama)
      {
        if (frameHeight > frameWidth)
        {
          if (!MathUtils.CheckPowerOfTwo(frameWidth))
          {
            Debug.LogWarning(
                "[VideoCapturePro::SetOutputSize] The width should be power of two in height > width case.");
            return false;
          }
        }
      }
      if (frameWidth == lastWidth && frameHeight == lastHeight)
      {
        return true;
      }

      lastWidth = frameWidth;
      lastHeight = frameHeight;

      if (isPanorama)
      {
        if (outputTexture != null)
        {
          Destroy(outputTexture);
        }

        outputTexture = new RenderTexture(frameWidth, frameHeight, 0);
        outputTexture.hideFlags = HideFlags.HideAndDontSave;

        if (externalTexture != null)
        {
          Destroy(externalTexture);
        }
        copyReverseMaterial = Resources.Load("Materials/CopyReverse") as Material;
        copyReverseMaterial.DisableKeyword("REVERSE_TOP_BOTTOM");
        copyReverseMaterial.DisableKeyword("REVERSE_LEFT_RIGHT");
        externalTexture = new RenderTexture(frameWidth, frameHeight, 0);
        externalTexture.hideFlags = HideFlags.HideAndDontSave;
        // Create render cubemap.
        panoramaTempRenderTexture = new RenderTexture(cubemapSize, cubemapSize, 24);
        panoramaTempRenderTexture.dimension = UnityEngine.Rendering.TextureDimension.Cube;
        panoramaTempRenderTexture.hideFlags = HideFlags.HideAndDontSave;
        // Setup camera as required for panorama capture.
        captureCamera.targetTexture = panoramaTempRenderTexture;
        if (captureGUI)
        {
          faceTempRenderTexture = new RenderTexture(cubemapSize, cubemapSize, 24);
          faceTempRenderTexture.isPowerOfTwo = true;
          faceTempRenderTexture.wrapMode = TextureWrapMode.Clamp;
          faceTempRenderTexture.filterMode = FilterMode.Bilinear;
          faceTempRenderTexture.autoGenerateMips = false;
        }
      }
      else
      {
        if (isDedicated)
        {
          frameRenderTexture = new RenderTexture(frameWidth, frameHeight, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
          // Make sure the rendertexture is created.
          frameRenderTexture.Create();
          captureCamera.targetTexture = frameRenderTexture;
        }
        else
        {
          if (frameRenderTexture != null)
          {
            frameRenderTexture.DiscardContents();
            RenderTexture.ReleaseTemporary(frameRenderTexture);
            frameRenderTexture = null;
          }
          if (frameRenderTexture == null)
          {
            frameRenderTexture = RenderTexture.GetTemporary(frameWidth, frameHeight, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default, 1);
          }
        }
      }
      if (stereo != StereoType.NONE)
      {
        // Init stereo video material.
        if (stereoPackMaterial == null)
        {
          stereoPackMaterial = new Material(Shader.Find("Evereal/Stereoscopic"));
        }
        stereoPackMaterial.hideFlags = HideFlags.HideAndDontSave;
        stereoPackMaterial.DisableKeyword("STEREOPACK_TOP");
        stereoPackMaterial.DisableKeyword("STEREOPACK_BOTTOM");
        stereoPackMaterial.DisableKeyword("STEREOPACK_LEFT");
        stereoPackMaterial.DisableKeyword("STEREOPACK_RIGHT");
        stereoPackMaterial.DisableKeyword("REVERSE");
        // Init stereo target texture.
        if (stereoTargetTexture != null)
        {
          Destroy(stereoTargetTexture);
        }
        stereoTargetTexture = new RenderTexture(frameWidth, frameHeight, 24);
        stereoTargetTexture.Create();
      }
      // Init final target texture.
      if (finalTargetTexture != null)
      {
        finalTargetTexture.DiscardContents();
        RenderTexture.ReleaseTemporary(finalTargetTexture);
        finalTargetTexture = null;
      }
      if (finalTargetTexture == null)
      {
        finalTargetTexture = RenderTexture.GetTemporary(frameWidth, frameHeight, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Default, 1);
      }
      // Init the copy material use hidden shader.
      blitMaterial = new Material(Shader.Find("Hidden/BlitCopy"));
      blitMaterial.hideFlags = HideFlags.HideAndDontSave;
      return true;
    }

    // Take screenshot
    public void Screenshot()
    {
      if (isPanorama)
      {
        if (!capturingStart && sceneCamera)
        {
          sceneCamera.transform.position = transform.position;
          sceneCamera.RenderToCubemap(panoramaTempRenderTexture); // render cubemap
        }
      }
      if (!SetOutputSize()) return;
      StartCoroutine(CaptureScreenshot());
    }

    private IEnumerator CaptureScreenshot()
    {
      // yield a frame to re-render into the rendertexture
      yield return new WaitForEndOfFrame();
      string screenshotPath = PathConfig.SaveFolder + StringUtils.GetJpgFileName(StringUtils.GetRandomString(5));
      Debug.LogFormat("[VideoCapturePro::Screenshot] Saved {0} x {1} screenshot: {2}", frameWidth, frameHeight, screenshotPath);
      if (isPanorama)
      {
        GPUCaptureLib_SaveScreenShot(externalTexture.GetNativeTexturePtr(), screenshotPath, false);
      }
      else
      {
        GPUCaptureLib_SaveScreenShot(frameRenderTexture.GetNativeTexturePtr(), screenshotPath, true);
      }
    }
    #endregion
    public void Cleanup()
    {
      GPUCaptureLib_MuxingClean();
    }
    #region Panorama Capture Core
    private void RenderCubeFace(CubemapFace face, float x, float y, float w, float h)
    {
      // texture coordinates for displaying each cube map face
      Vector3[] faceTexCoords =
      {
            // +x
            new Vector3(1, 1, 1),
            new Vector3(1, -1, 1),
            new Vector3(1, -1, -1),
            new Vector3(1, 1, -1),
            // -x
            new Vector3(-1, 1, -1),
            new Vector3(-1, -1, -1),
            new Vector3(-1, -1, 1),
            new Vector3(-1, 1, 1),

            // -y
            new Vector3(-1, -1, 1),
            new Vector3(-1, -1, -1),
            new Vector3(1, -1, -1),
            new Vector3(1, -1, 1),
            // +y // flipped with -y for fb live
            new Vector3(-1, 1, -1),
            new Vector3(-1, 1, 1),
            new Vector3(1, 1, 1),
            new Vector3(1, 1, -1),

            // +z
            new Vector3(-1, 1, 1),
            new Vector3(-1, -1, 1),
            new Vector3(1, -1, 1),
            new Vector3(1, 1, 1),
            // -z
            new Vector3(1, 1, -1),
            new Vector3(1, -1, -1),
            new Vector3(-1, -1, -1),
            new Vector3(-1, 1, -1),
            };

      GL.PushMatrix();
      GL.LoadOrtho();
      GL.LoadIdentity();

      int i = (int)face;

      GL.Begin(GL.QUADS);
      GL.TexCoord(faceTexCoords[i * 4]); GL.Vertex3(x, y, 0);
      GL.TexCoord(faceTexCoords[i * 4 + 1]); GL.Vertex3(x, y + h, 0);
      GL.TexCoord(faceTexCoords[i * 4 + 2]); GL.Vertex3(x + w, y + h, 0);
      GL.TexCoord(faceTexCoords[i * 4 + 3]); GL.Vertex3(x + w, y, 0);
      GL.End();

      GL.PopMatrix();
    }

    private void SetMaterialParameters(Material material)
    {
      // convert to equirectangular
      material.SetTexture("_CubeTex", panoramaTempRenderTexture);
      material.SetVector("_SphereScale", sphereScale);
      material.SetVector("_SphereOffset", sphereOffset);

      if (includeCameraRotation)
      {
        // cubemaps are always rendered along axes, so we do rotation by rotating the cubemap lookup
        material.SetMatrix("_CubeTransform", Matrix4x4.TRS(Vector3.zero, transform.rotation, Vector3.one));
      }
      else
      {
        material.SetMatrix("_CubeTransform", Matrix4x4.identity);
      }
    }

    private void DisplayCubeMap(RenderTexture dest)
    {
      SetMaterialParameters(outputCubemapMaterial);
      outputCubemapMaterial.SetPass(0);

      Graphics.SetRenderTarget(outputTexture);

      float s = 1.0f / 3.0f;
      RenderCubeFace(CubemapFace.PositiveX, 0.0f, 0.5f, s, 0.5f);
      RenderCubeFace(CubemapFace.NegativeX, s, 0.5f, s, 0.5f);
      RenderCubeFace(CubemapFace.PositiveY, s * 2.0f, 0.5f, s, 0.5f);

      RenderCubeFace(CubemapFace.NegativeY, 0.0f, 0.0f, s, 0.5f);
      RenderCubeFace(CubemapFace.PositiveZ, s, 0.0f, s, 0.5f);
      RenderCubeFace(CubemapFace.NegativeZ, s * 2.0f, 0.0f, s, 0.5f);

      Graphics.SetRenderTarget(null);
      Graphics.Blit(outputTexture, externalTexture);
      Graphics.Blit(outputTexture, dest);
    }

    private void DisplayEquirect(RenderTexture dest)
    {
      SetMaterialParameters(convertMaterial);
      Graphics.Blit(null, externalTexture, convertMaterial);
      Graphics.Blit(externalTexture, dest);
    }
    #endregion

    #region Dll Import
    [DllImport("GPUCaptureLib", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
    private static extern void GPUCaptureLib_StartEncoding(IntPtr texture, string path, bool isLive, int fps, bool needFlipping, bool withAudio);
    [DllImport("GPUCaptureLib", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
    private static extern void GPUCaptureLib_AudioEncoding();
    [DllImport("GPUCaptureLib", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
    private static extern void GPUCaptureLib_StopEncoding();
    [DllImport("GPUCaptureLib", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
    private static extern void GPUCaptureLib_MuxingData(string path, string savePath, string audioPath, string projectPath);
    [DllImport("GPUCaptureLib", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
    private static extern void GPUCaptureLib_MuxingClean();
    [DllImport("GPUCaptureLib", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
    private static extern void GPUCaptureLib_StartLiveStream(string url, string ffpath, int fps);
    [DllImport("GPUCaptureLib", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
    private static extern void GPUCaptureLib_StopLiveStream();
    [DllImport("GPUCaptureLib", CharSet = CharSet.Unicode, CallingConvention = CallingConvention.StdCall)]
    private static extern void GPUCaptureLib_SaveScreenShot(IntPtr texture, string path, bool needFlipping);
    #endregion
  }
}