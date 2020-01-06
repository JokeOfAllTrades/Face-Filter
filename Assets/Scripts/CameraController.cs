﻿using UnityEngine;
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
    private static int destroyPort = 5058;
    private GameObject plane;
    private int[] sides = new int[4] { 0, 0, 0, 0 };
    private byte[] imageData;
    private Texture2D flatScreen;
    private static String anacondaDirectory = "C:\\Users\\DJ\\Anaconda3\\Scripts";
    private static String anacondaCommand = anacondaDirectory + "\\activate.bat";
    private static String projectDirectory = "C:\\Users\\DJ\\Documents\\Development\\Unity Games\\Face Filter\\Assets\\Face Detection";
    private static String pythonCommand = "python \"" 
        + projectDirectory + "\\facedetector.py\" " + "--prototxt \"" 
        + projectDirectory + "\\deploy.prototxt.txt\" " + "--model \"" 
        + projectDirectory + "\\res10_300x300_ssd_iter_140000.caffemodel\"";
    private Process process;
    private UdpClient killSwitch;
   

    void Start()
    {
        plane = GameObject.Find("Plane");
        flatScreen = new Texture2D(400, 300);
        killSwitch = new UdpClient();
        InitiatePython();
        InitiateConnection();
        //Thread.Sleep(20000);
        InitiateThreads();
    }

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
        
        IPEndPoint endpoint = null;
        try
        {
            endpoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), dataPort);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.Log(e.ToString());
        }

        dataThreadContinue = true;
        while (dataThreadContinue)
        {
            sides = new int[4] { 0, 0, 0, 0 };
            try
            {
                // currently, you use datagrams to carry images which limits your largest
                // image to 65 kilobytes. consider using some sort of length-prefixed protocol
                // to make it more extensible just in case
                
                for (int i = 0; i <= 3; i++)
                {
                    var pieces = client.Receive(ref endpoint);
                    for (int j = 0; j <= pieces.Length - 1; j++)
                    {
                        sides[i] += (int)pieces[j] * (int)Mathf.Pow(16, j * 2);                        
                    }
                    UnityEngine.Debug.Log(sides[i]);
                }
                UnityEngine.Debug.Log("\n");
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
                UnityEngine.Debug.Log(e.ToString());
            }
        }
        client.Dispose();
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
        byte[] q = { (byte)'q' };
        killSwitch.Send(q,1);
        dataThreadContinue = false;
        imageThreadContinue = false;
        killSwitch.Dispose();
        process.CloseMainWindow();
        process.Close();
        
        process.Dispose();
    }
}