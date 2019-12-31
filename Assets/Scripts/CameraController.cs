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
    private static int dataPort = 5056;
    private static int imagePort = 5057;
    private GameObject plane;
    private byte[] sides = new byte[4] { 0, 0, 0, 0 };
    private byte[] imageData;
    private Texture2D flatScreen;
    private static String anacondaDirectory = "C:\\Users\\DJ\\Anaconda3\\Scripts";
    private static String anacondaCommand = anacondaDirectory + "\\activate.bat";
    private static String pythonCommand = "python \"C:\\Users\\DJ\\Documents\\Development\\Unity Games\\Face Filter\\Assets\\Face Detection\\detectfacesvideo.py\" " +
        "--prototxt \"C:\\Users\\DJ\\Documents\\Development\\Unity Games\\Face Filter\\Assets\\Face Detection\\deploy.prototxt.txt\" " +	
        "--model \"C:\\Users\\DJ\\Documents\\Development\\Unity Games\\Face Filter\\Assets\\Face Detection\\res10_300x300_ssd_iter_140000.caffemodel\"";


    void Start()
    {

        //UnityEngine.Debug.Log(anacondaCommand + "\n");
        //UnityEngine.Debug.Log(pythonCommand + "\n");
        //dataPort = ;
        //imagePort = ;
        plane = GameObject.Find("Plane");
        flatScreen = new Texture2D(400, 300);
        InitiatePython();
        InitiateThreads();
        
    }
    private void InitiatePython()
    {
  
        var process = new Process
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
        process.WaitForExit();
    }
    
    private void InitiateThreads()
    {
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

        while (true)
        {
            try
            {
                byte[] pieces = client.Receive(ref endpoint);
                for (int i = 0; i <= 3; i++)
                {
                /*                
                sides[0] = pieces[0];
                sides[1] = pieces[4];
                sides[2] = pieces[8];
                sides[3] = pieces[12];
                */
                    foreach (byte part in pieces)
                        sides[i] += part;
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
        while (true)
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
    }

    private IPEndPoint SetSocket(UdpClient client, int port)
    {
        IPEndPoint endpoint = null;
        try
        {
            endpoint = new IPEndPoint(IPAddress.Parse("0.0.0.0"), port);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.Log(e.ToString());
            return null;
        }

        return endpoint;
    }
    //sides[0], sides[1], sides[2], sides[3]

    void Update()
    {
        if (imageData != null)
        {
            
            ImageConversion.LoadImage(flatScreen,imageData,false);
            
            DrawSideX(sides[0], sides[2], sides[1]);
            DrawSideX(sides[0], sides[2], sides[3]);
            DrawSideY(sides[1], sides[3], sides[0]);
            DrawSideY(sides[1], sides[3], sides[2]);
            
            
            plane.GetComponent<Renderer>().material.mainTexture = flatScreen;
        }

    }
    void DrawSideX(int start, int end, int y)
    {
        for (int i = start; i <= end; i++)
        {
            flatScreen.SetPixel(i, y, UnityEngine.Color.black);
            flatScreen.Apply();
        }
    }
    void DrawSideY(int start, int end, int x)
    {
        for (int i = start; i <= end; i++)
        {
            flatScreen.SetPixel(x, i, UnityEngine.Color.black);
            flatScreen.Apply();
        }
    }
}