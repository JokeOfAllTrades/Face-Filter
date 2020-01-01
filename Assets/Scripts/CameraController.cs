using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.IO;
using System.Drawing;
using System.Diagnostics;

public class CameraController : MonoBehaviour
{
    private Thread recieveDataThread;
    private Thread recieveImageThread;
    private bool dataThreadContinue;
    private bool imageThreadContinue;
    private static int dataPort = 5056;
    private static int imagePort = 5057;
    private GameObject plane;
    private byte[] sides = new byte[4] { 0, 0, 0, 0 };
    private byte[] imageData;
    private Texture2D flatScreen;
    private static String anacondaDirectory = "C:\\Users\\DJ\\Anaconda3\\Scripts";
    private static String anacondaCommand = anacondaDirectory + "\\activate.bat";
    private static String projectDirectory = "C:\\Users\\DJ\\Documents\\Development\\Unity Games\\Face Filter\\Assets\\Face Detection";
    private static String pythonCommand = "python \"" + projectDirectory + "\\detectfacesvideo.py\" " +
        "--prototxt \"" + projectDirectory + "\\deploy.prototxt.txt\" " +	
        "--model \"" + projectDirectory + "\\res10_300x300_ssd_iter_140000.caffemodel\"";
    private Process process;

    void Start()
    {
        plane = GameObject.Find("Plane");
        flatScreen = new Texture2D(400, 300);
        InitiatePython();
        InitiateThreads();        
    }
    private void InitiatePython()
    {
        process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                RedirectStandardInput = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                WorkingDirectory = anacondaDirectory
            }
        };
        process.Start();
        using (var sw = process.StandardInput)
        {
            if (sw.BaseStream.CanWrite)
            {
                sw.WriteLine(anacondaCommand);
                sw.WriteLine(pythonCommand);
            }
        }
    }
    
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
        var client = new UdpClient(dataPort);
        var throwaway_ep = new IPEndPoint(IPAddress.Any, 0);
        
        dataThreadContinue = true;
        while (dataThreadContinue)
        {
            try
            {
                // currently, you use datagrams to carry images which limits your largest
                // image to 65 kilobytes. consider using some sort of length-prefixed protocol
                // to make it more extensible just in case
                byte[] pieces = client.Receive(ref throwaway_ep);

                for (int i = 0; i <= 3; i++)
                {
                    for (int j = 0; i <= 3; i++)
                        sides[i] += pieces[i * 4 + j];
                }
                
            }
            catch (Exception e)
            {
                UnityEngine.Debug.Log(e.ToString());
            }
        }
    }

    private void ReceiveImage()
    {
        var client = new UdpClient(imagePort);
        var throwaway_ep = new IPEndPoint(IPAddress.Any, 0);
        
        imageThreadContinue = true;
        while (imageThreadContinue)
        {
            try
            {
                // currently, you use datagrams to carry images which limits your largest
                // image to 65 kilobytes. consider using some sort of length-prefixed protocol
                // to make it more extensible just in case
                imageData = client.Receive(ref throwaway_ep);
            }
            catch (Exception e)
            {
                UnityEngine.Debug.Log(e.ToString());
            }
        }
    }

    void Update()
    {
        if (imageData != null)
        {
            ImageConversion.LoadImage(flatScreen,imageData,false);
            
            DrawSideX(sides[0], sides[2], sides[1]);
            DrawSideX(sides[0], sides[2], sides[3]);
            DrawSideY(sides[1], sides[3], sides[0]);
            DrawSideY(sides[1], sides[3], sides[2]);
            flatScreen.Apply();
            plane.GetComponent<Renderer>().material.mainTexture = flatScreen;
        }
    }

    void DrawSideX(int start, int end, int y)
    {
        for (int i = start; i <= end; i++)
        {
            flatScreen.SetPixel(i, y, UnityEngine.Color.black);
        }
    }
    
    void DrawSideY(int start, int end, int x)
    {
        for (int i = start; i <= end; i++)
        {
            flatScreen.SetPixel(x, i, UnityEngine.Color.black);
        }
    }

    void OnDestroy()
    {
        // Send q to window
        process.CloseMainWindow();
        process.Close();
        process.Dispose();

        dataThreadContinue = false;
        imageThreadContinue = false;
    }
}
