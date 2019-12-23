using UnityEngine;
using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;



public class CameraController: MonoBehaviour
{
    private Thread receiveThread;
    private UdpClient client;
    private int port;
    private WebCamTexture webCamera;
    private GameObject plane;
    private Rect frame;
    private byte[] data = new byte[4] { 0, 0, 0, 0 };

    void Start ()
    { 
        port = 5065;
		InitUDP();
        InitCam();
    }

    private void InitCam()
    {
        webCamera = new WebCamTexture();
        plane = GameObject.FindWithTag("Plane");

        plane.GetComponent<Renderer>().material.mainTexture = webCamera;
        webCamera.Play();
    }

	private void InitUDP()
	{
		receiveThread = new Thread (new ThreadStart(ReceiveData));
		receiveThread.IsBackground = true;
		receiveThread.Start ();
	}

	private void ReceiveData()
	{
		client = new UdpClient (port);
        IPEndPoint anyIP = null;
        try
        {
            anyIP = new IPEndPoint(IPAddress.Parse("0.0.0.0"), port);
		} catch (Exception e)
		{
		    Debug.Log(e.ToString());
		}	
		while (true)
		{
			try
			{
                byte[] data = new byte[4] { 0,0,0,0 };
			
                for (int i = 0; i <= 3; i++)
				{
				    byte[] pieces = client.Receive(ref anyIP);
					foreach (byte part in pieces)
                    data[i] += part;
				}
			} catch(Exception e)
			{
                Debug.Log(e.ToString());
			}
		}
	}



    void OnGUI() => GUI.Box(new Rect(data[0], data[1], data[2], data[3]), "test");


    void Update () 
	{

	}
}
