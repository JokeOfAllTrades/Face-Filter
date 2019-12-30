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
        // is this class used at all?
    }
    
    void Start()
    {
        dataPort = 5056;
        imagePort = 5057;
        flatScreen = new Texture2D(400, 300);
        plane = GameObject.Find("Plane");
        
        InitUDP();
    }

    private void InitUDP()
    {
        // if this needs to be commented in and out frequently, consider making it configurable
        // instead of needing recompilation. if this needs to be commented out permanently, consider
        // removing it outright
        
        /*
        recieveDataThread = new Thread(new ThreadStart(ReceiveData));
        recieveDataThread.IsBackground = true;
        recieveDataThread.Start();
        */
        
        // really consider using async stuff here
        recieveImageThread = new Thread(new ThreadStart(ReceiveImage));
        recieveImageThread.IsBackground = true;
        recieveImageThread.Start();
    }

    private void ReceiveData()
    {
        // is this method vestigial code? consider removing it, see above
        var throwaway_ep = new IPEndPoint(IPAddress.Any, 0);
        
        // instead of while(true), consider making these threads cancellable
        // while (Running) with a boolean Running property might be a good start
        while (true)
        {
            try
            {
                byte[] pieces = client.Receive(ref throwaway_ep);
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
        var client = new UdpClient(imagePort);
        var throwaway_ep = new IPEndPoint(IPAddress.Any, 0);
        
        // instead of while(true), consider making these threads cancellable
        // while (Running) with a boolean Running property might be a good start
        while (true)
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
    //sides[0], sides[1], sides[2], sides[3]

    void Update()
    {
        if (imageData != null)
        {
            // repeating my vestigial code question
            /*
            DrawSideX(sides[0], sides[2], sides[1]);
            DrawSideX(sides[0], sides[2], sides[3]);
            DrawSideY(sides[1], sides[3], sides[0]);
            DrawSideY(sides[1], sides[3], sides[2]);
            flatScreen.Apply();
            */
            
            ImageConversion.LoadImage(flatScreen,imageData,false);
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
