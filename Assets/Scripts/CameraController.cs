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

    private int dataPort;
    private int imagePort;
    private GameObject plane;
    private Rect frame;
    private byte[] sides = new byte[4] { 0, 0, 0, 0 };
    private byte[] imageData;
    private Image image;
    private Texture2D flatScreen;
    private class PythonController : Process
    {

    }


    void Start()
    {
        dataPort = 5056;
        imagePort = 5057;
        flatScreen = new Texture2D(400, 300);
        InitUDP();
        plane = GameObject.Find("Plane");
    }

    private void InitUDP()
    {
        /*
        recieveDataThread = new Thread(new ThreadStart(ReceiveData));
        recieveDataThread.IsBackground = true;
        recieveDataThread.Start();
        */
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
                sides[0] = pieces[0];
                sides[1] = pieces[4];
                sides[2] = pieces[8];
                sides[3] = pieces[12];
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
            /*
            DrawSideX(sides[0], sides[2], sides[1]);
            DrawSideX(sides[0], sides[2], sides[3]);
            DrawSideY(sides[1], sides[3], sides[0]);
            DrawSideY(sides[1], sides[3], sides[2]);
            flatScreen.Apply();
            */
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
}