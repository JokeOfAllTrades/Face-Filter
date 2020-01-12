using UnityEngine;
using UnityEditor;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Drawing;
using System.Diagnostics;



public class CameraController : MonoBehaviour
{   
    // threads and cancels: image thread gets image data, data thread gets a square the face fits into
    private Thread recieveDataThread;
    private Thread recieveImageThread;
    private bool dataThreadContinue;
    private bool imageThreadContinue;
    private static int dataPort = 5056;
    private static int imagePort = 5057;
    // port that helps shut down the python process
    private static int destroyPort = 5058;
    // planeScreen screen holds the texture the camera transmits to planeOverley holds the texture of what ever images fit on top of it
    private GameObject planeScreen;
    private GameObject planeOverley;
    private Camera mainCamera;
    private float pixelToUnitFactor;
    // used to adjust the y value of the mask to fit the face
    public float yOffset = 3.42f;
    // the middle point of the camera screen
    private float middlePixel = 150;
    // the difference between the python script's x = 0 and c sharp's
    private float xGap = 50;
    private Texture2D mask;
    private float baseMaskWidth;
    private float baseMaskHeight;
    private Texture2D flatScreen;
    // used to hold the data sent by the data thread
    private int[] sides;
    private byte[] imageData;
    private static String anacondaDirectory = "C:\\Users\\DJ\\Anaconda3\\Scripts";
    private static String anacondaCommand = anacondaDirectory + "\\activate.bat";
    private static String projectDirectory = "C:\\Users\\DJ\\Documents\\Development\\Unity Games\\Face Filter\\Assets\\Face Detection";
    private static String pythonCommand = "python \""
        + projectDirectory + "\\facedetector.py\" " + "--prototxt \""
        + projectDirectory + "\\deploy.prototxt.txt\" " + "--model \""
        + projectDirectory + "\\res10_300x300_ssd_iter_140000.caffemodel\"";
    // the process used to launch the python script
    private Process process;
    // used to terminate the python script
    private UdpClient killSwitch;

    void Start()
    {
        planeScreen = GameObject.Find("PlaneScreen");
        planeOverley = GameObject.Find("PlaneOverley");
        baseMaskWidth = planeOverley.GetComponent<Transform>().localScale.x;
        baseMaskHeight = planeOverley.GetComponent<Transform>().localScale.y;
        mainCamera = GameObject.Find("Main Camera").GetComponent<Camera>();
        pixelToUnitFactor = mainCamera.orthographicSize * 2 / mainCamera.pixelHeight;
        //planeOverley.GetComponent<Renderer>().enabled = false;
        //flat screen should be converted to a render texture and the image data should be sent to the camera
        flatScreen = new Texture2D(400, 300);

        killSwitch = new UdpClient();
        InitiatePython();
        InitiateConnection();
        InitiateThreads();
    }

    // connects to the python port listening for the quit command
    void InitiateConnection()
    {
        IPEndPoint endpoint = null;
        try
        {
            endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), destroyPort);
            killSwitch.Connect(endpoint);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.Log(e.ToString());
        }
    }

    // lahnches the python script
    private void InitiatePython()
    {
        process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = "/k",
                RedirectStandardInput = true,
                UseShellExecute = false,
                //RedirectStandardOutput = true,
                //CreateNoWindow = true,
                WorkingDirectory = anacondaDirectory,
            }
        };
        process.Start();

        var inputStream = process.StandardInput;
        if (inputStream.BaseStream.CanWrite)
        {
            inputStream.WriteLine(anacondaCommand);
            inputStream.WriteLine(pythonCommand);
        }

    }

    // starts the listener threads
    private void InitiateThreads()
    {
        // really consider using async stuff here
        recieveDataThread = new Thread(new ThreadStart(ReceiveData));
        recieveDataThread.IsBackground = true;
        recieveDataThread.Start();
        recieveImageThread = new Thread(new ThreadStart(ReceiveImage));
        recieveImageThread.IsBackground = true;
        recieveImageThread.Start();
    }

    private void ReceiveData()
    {
        UdpClient client = new UdpClient(dataPort);
        sides = new int[4] { 150, 150, 150, 150 };
        IPEndPoint endpoint = null;
        try
        {
            endpoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), dataPort);
        }
        catch (Exception e)
        {
            Thread.ResetAbort();
            UnityEngine.Debug.Log(e.ToString());
        }

        dataThreadContinue = true;
        while (dataThreadContinue)
        {
            // holds a local copy of the sides array
            int[] oldSides = new int[4] { 0, 0, 0, 0 };
            try
            {
                // currently, you use datagrams to carry images which limits your largest
                // image to 65 kilobytes. consider using some sort of length-prefixed protocol
                // to make it more extensible just in case

                for (int i = 0; i <= 3; i++)
                {
                    // each side is made up by adding four bytes.  each byte is multiplied by a factor of 16^(j x 2)
                    var pieces = client.Receive(ref endpoint);
                    for (int j = 0; j <= pieces.Length - 1; j++)
                    {
                        oldSides[i] += (int)pieces[j] * (int)Mathf.Pow(16, j * 2);
                    }
                }
                Array.Copy(oldSides, sides, 4);
            }
            catch (Exception e)
            {
                Thread.ResetAbort();
                UnityEngine.Debug.Log(e.ToString());
            }
        }
        client.Dispose();
    }

    private void ReceiveImage()
    {
        UdpClient client = new UdpClient(imagePort);

        IPEndPoint endpoint = null;
        try
        {
            endpoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), imagePort);
        }
        catch (Exception e)
        {
            Thread.ResetAbort();
            UnityEngine.Debug.Log(e.ToString());
        }

        imageThreadContinue = true;
        while (imageThreadContinue)
        {
            try
            {
                imageData = client.Receive(ref endpoint);
            }
            catch (Exception e)
            {
                Thread.ResetAbort();
                UnityEngine.Debug.Log(e.ToString());
            }
        }
        client.Dispose();
    }

    void SetMask(int[] sides, Transform maskTransform)
    {
        // xGap is subtracted as the sent values are for a 400 x 300 screen
        float xMin = sides[0] - xGap;
        float yMin = sides[1];
        float xMax = sides[2] - xGap;
        float yMax = sides[3];
        // Taking the average gets the middle point in pixels
        float xMid = (xMin + xMax) / 2;
        float yMid = (yMin + yMax) / 2;

        // Gets the amount we will scale the mask by
        float faceWidth = Mathf.Abs(xMin - xMax);

        // Converts the middle point to be relative to the middle of the screen
        xMid -= middlePixel;
        yMid -= middlePixel;

        // Converts the middle to Unity units
        xMid *= pixelToUnitFactor;
        yMid *= pixelToUnitFactor;
        faceWidth *= pixelToUnitFactor * .25f;

        // Scales the mask for how for away the face is
        maskTransform.localScale = new Vector3(faceWidth * baseMaskWidth, faceWidth * baseMaskHeight, maskTransform.localScale.z);

        // Places the mask on top of the head instead of the middle
        float stretch = (faceWidth * baseMaskHeight - baseMaskHeight) / 2;
        yMid = yMid + yOffset;

        // Positions the mask based on where the face is
        maskTransform.position = new Vector3(xMid, yMid, 0);
        //UnityEngine.Debug.Break();

    }

    void Update()
    {
        if (imageData != null)
        {
            // triggers only after sides has been initialized
            if (sides != null && sides[0] != 150 && sides[2] != 150)
            {
                SetMask(sides, planeOverley.GetComponent<Transform>());
                planeScreen.GetComponent<Renderer>().enabled = true;
            }
            ImageConversion.LoadImage(flatScreen, imageData, false);
            planeScreen.GetComponent<Renderer>().material.mainTexture = flatScreen;
        }
    }

    void OnDestroy()
    {
        // cancels threads
        dataThreadContinue = false;
        imageThreadContinue = false;
        // sends quit signal to python and disposes of remaining objects
        byte[] q = { (byte)'q' };
        killSwitch.Send(q,1);
        killSwitch.Dispose();
        process.CloseMainWindow();
        process.Close();
        
        process.Dispose();
    }
}