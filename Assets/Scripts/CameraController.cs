using UnityEngine;
using UnityEditor;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Drawing;
using System.Diagnostics;


namespace JokeOfAllTrades.FaceFilter.Primary
{
    public class CameraController : MonoBehaviour
    {
        // threads and cancels: image thread gets image data, data thread gets a square the face fits into
        private Thread recieveDataThread;
        private Thread recieveImageThread;
        private bool dataThreadContinue = true;
        private bool imageThreadContinue = true;
        // Prevents the data thread from assigning face boundaries when true
        private bool dataThreadLock = false;
        private static int controlPort = 5056;
        private static int dataPort = 5056;
        private static int imagePort = 5058;
        // port that helps shut down the python process
        private static int destroyPort = 5059;
        // planeScreen screen holds the texture the camera transmits to planeOverley holds the texture of what ever images fit on top of it
        private GameObject planeScreen;
        private GameObject planeOverley;
        private Camera mainCamera;
        private float pixelToUnitFactor;
        // used to adjust the y value of the mask to fit the face
        public float yOffset = 3.42f;
        private int  xMiddlePixel = 200;
        private int yMiddlePixel = 150;
        private float baseMaskWidth;
        private float baseMaskHeight;
        private Texture2D flatScreen;
        // used to hold the data sent by the data thread
        private int[] sides;
        private byte[] imageData;
        // for use by python/anaconda
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
        private AudioClip sounds;
        private WebCamTexture camTexture;

        
        void Start()
        {
            planeScreen = GameObject.Find("PlaneScreen");
            planeOverley = GameObject.Find("PlaneOverley");
            baseMaskWidth = planeOverley.GetComponent<Transform>().localScale.x;
            baseMaskHeight = planeOverley.GetComponent<Transform>().localScale.y;
            mainCamera = GameObject.Find("Main Camera").GetComponent<Camera>();
            pixelToUnitFactor = mainCamera.orthographicSize * 2 / mainCamera.pixelHeight;
            planeScreen.transform.localScale = new Vector3(xMiddlePixel * 2 * pixelToUnitFactor / 10, 1, yMiddlePixel * 2 * pixelToUnitFactor / 10);
            sides = new int[4] { xMiddlePixel, yMiddlePixel, xMiddlePixel, yMiddlePixel };
            planeOverley.GetComponent<Renderer>().enabled = false;

            camTexture = new WebCamTexture(300, 300);
            camTexture.Play();
            flatScreen = new Texture2D(camTexture.width, camTexture.height);
            planeScreen.GetComponent<Renderer>().material.mainTexture = camTexture;
            sounds = Microphone.Start("", false, 200, 48000);
            mainCamera.GetComponent<AudioSource>().clip = sounds;
            mainCamera.GetComponent<AudioSource>().Play();

            killSwitch = new UdpClient();
            InitiatePython();
            InitiateConnection();
            InitiateThreads();
        }

        // launches the python script
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

        // starts the listener threads
        private void InitiateThreads()
        {
            // really consider using async stuff here
            recieveDataThread = new Thread(new ThreadStart(ReceiveData));
            recieveDataThread.IsBackground = true;
            recieveDataThread.Start();
            recieveImageThread = new Thread(new ThreadStart(SendImage));
            recieveImageThread.IsBackground = true;
            recieveImageThread.Start();
        }

        private void ReceiveData()
        {
            UdpClient client = new UdpClient(dataPort);

            IPEndPoint endpoint = null;
            try
            {
                endpoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), dataPort);
            }
            catch (Exception e)
            {
                if (Thread.CurrentThread.ThreadState == System.Threading.ThreadState.Aborted)
                {
                    Thread.ResetAbort();
                }
                UnityEngine.Debug.Log(e.ToString());
            }

            while (dataThreadContinue)
            {
                // holds a local copy of the sides array
                int[] oldSides = new int[4] { 0, 0, 0, 0 };
                try
                {
                    // currently, you use datagrams to carry images which limits your largest
                    // image to 65 kilobytes. consider using some sort of length-prefixed protocol
                    // to make it more extensible just in case
                    while (dataThreadLock == true)
                    {
                        Thread.Sleep(100);
                    }
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
                    if (Thread.CurrentThread.ThreadState == System.Threading.ThreadState.Aborted)
                    {
                        Thread.ResetAbort();
                    }
                    UnityEngine.Debug.Log(e.ToString());
                }
            }
            client.Dispose();
        }

        private void SendImage()
        {
            UdpClient client = new UdpClient();
            TcpClient control = new TcpClient();

            IPEndPoint endpoint = null;
            IPEndPoint tcp = null;
            bool connectionMade = false;
            try
            {
                endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), imagePort);
                client.Connect(endpoint);
            }
            catch (SocketException e)
            {
                if (Thread.CurrentThread.ThreadState == System.Threading.ThreadState.Aborted)
                {
                    Thread.ResetAbort();
                }
                UnityEngine.Debug.Log(e.ToString());
            }
            catch (Exception e)
            {
                if (Thread.CurrentThread.ThreadState == System.Threading.ThreadState.Aborted)
                {
                    Thread.ResetAbort();
                }
                UnityEngine.Debug.Log(e.ToString());
            }
            try
            {
                tcp = new IPEndPoint(IPAddress.Parse("127.0.0.1"), controlPort);
            }
            catch (Exception e)
            {    
                if (Thread.CurrentThread.ThreadState == System.Threading.ThreadState.Aborted)
                {
                    Thread.ResetAbort();
                }
                UnityEngine.Debug.Log(e.ToString());
            }

            while(connectionMade != true)
            {
                try
                {
                    control.Connect(tcp);
                    connectionMade = true;
                }
                catch (Exception e)
                {
                    if (Thread.CurrentThread.ThreadState == System.Threading.ThreadState.Aborted)
                    {
                        Thread.ResetAbort();
                    }
                    Thread.Sleep(100);
                }
            }

            while (imageThreadContinue)
            {
                try
                {
                    if (imageData != null)
                    {
                        byte[] size = BitConverter.GetBytes(imageData.Length);
                        NetworkStream stream = control.GetStream();
                        stream.Write(size, 0, 4);
                        client.Send(imageData, imageData.Length);
                    }
                }
                catch (Exception e)
                {
                    if(Thread.CurrentThread.ThreadState == System.Threading.ThreadState.Aborted)
                    {
                        Thread.ResetAbort();
                    }
                    UnityEngine.Debug.Log(e.ToString());
                    try
                    {
                        endpoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), imagePort);
                        client.Connect(endpoint);
                    }
                    catch (Exception x)
                    {
                        if (Thread.CurrentThread.ThreadState == System.Threading.ThreadState.Aborted)
                        {
                            Thread.ResetAbort();
                        }
                        UnityEngine.Debug.Log(x.ToString());
                    }
                }
            }
            client.Dispose();
            control.Dispose();
        }

        void SetMask(int[] boundaries, Transform maskTransform)
        {
            // Prevents the data thread from altering our values during assignment
            dataThreadLock = true;
            float xMin = boundaries[0];
            float yMin = boundaries[1];
            float xMax = boundaries[2];
            float yMax = boundaries[3];
            dataThreadLock = false;

            // Centering around the middle pixel
            xMin -= xMiddlePixel;
            yMin -= yMiddlePixel;
            xMax -= xMiddlePixel;
            yMax -= yMiddlePixel;

            // Taking the average gets the middle point in pixels
            float xMid = (xMin + xMax) / 2;
            float yMid = (yMin + yMax) / 2;

            // Converts everything to Unity units and flips the y axis
            xMin *= pixelToUnitFactor;
            yMin *= -pixelToUnitFactor;
            xMax *= pixelToUnitFactor;
            yMax *= -pixelToUnitFactor;
            xMid *= pixelToUnitFactor;
            yMid *= -pixelToUnitFactor;

            // Gets the amount we will scale the mask by
            float faceFactor = xMax - xMin;

            /*
            UnityEngine.Debug.DrawLine(new Vector3(0, 0, 0), new Vector3(0, 3, 0), UnityEngine.Color.white, 1);
            UnityEngine.Debug.DrawLine(new Vector3(0, 0, 0), new Vector3(4, 0, 0), UnityEngine.Color.white, 1);
            UnityEngine.Debug.DrawLine(new Vector3(0, 0, 0), new Vector3(-4, 0, 0), UnityEngine.Color.white, 1);
            UnityEngine.Debug.DrawLine(new Vector3(0, 0, 0), new Vector3(0, -3, 0), UnityEngine.Color.white, 1);        
               
            UnityEngine.Debug.DrawLine(new Vector3(xMin, yMax, 0), new Vector3(xMin, yMin, 0), UnityEngine.Color.black, 1);
            UnityEngine.Debug.DrawLine(new Vector3(xMax, yMin, 0), new Vector3(xMax, yMax, 0), UnityEngine.Color.black, 1);
            UnityEngine.Debug.DrawLine(new Vector3(xMax, yMax, 0), new Vector3(xMin, yMax, 0), UnityEngine.Color.black, 1);
            UnityEngine.Debug.DrawLine(new Vector3(xMin, yMin, 0), new Vector3(xMax, yMin, 0), UnityEngine.Color.black, 1);
            */

            // Scales the mask to the size of the face
            maskTransform.localScale = new Vector3(faceFactor * baseMaskWidth, faceFactor * baseMaskHeight, maskTransform.localScale.z);

            // Places the mask on top of the head instead of the middle
            float stretch = (faceFactor * baseMaskHeight - baseMaskHeight) / 2;
            yMid = yMid + yOffset + stretch;

            // Positions the mask based on where the face is
            maskTransform.position = new Vector3(xMid, yMid, 0);
        }

        void Update()
        {
            if (camTexture.didUpdateThisFrame == true)
            { 
                flatScreen.SetPixels32(camTexture.GetPixels32());
                imageData = ImageConversion.EncodeToJPG(flatScreen);
            }

            // triggers only after sides has been initialized
            if (sides != null && sides[0] != 150 && sides[2] != 150)
            {
                SetMask(sides, planeOverley.GetComponent<Transform>());
                planeOverley.GetComponent<Renderer>().enabled = true;
            }
        }

        void OnDestroy()
        {
            Microphone.End("");
            // cancels threads
            dataThreadContinue = false;
            imageThreadContinue = false;
            // sends quit signal to python and disposes of remaining objects
            byte[] q = { (byte)'q' };
            killSwitch.Send(q, 1);
            killSwitch.Dispose();
            process.CloseMainWindow();
            process.Close();
            process.Dispose();
        }
    }
}