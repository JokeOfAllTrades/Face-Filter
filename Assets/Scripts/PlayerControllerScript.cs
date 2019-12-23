using UnityEngine;
using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public class PlayerControllerScript: MonoBehaviour
{
	Thread receiveThread;
	UdpClient client;
	int port;

	public GameObject Player;
	void Start () 
	{
		port = 5065;
		InitUDP();
	}

	private void InitUDP()
	{
		print ("UDP Initialized");

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
            print(e.ToString());
        }

        while (true)
		{
			try
			{
                byte[] data = new byte[4] { 0,0,0,0 };

                for (int i = 0; i < 4; i++)
                {
                    byte[] pieces = client.Receive(ref anyIP); 
                    foreach (byte part in pieces)
                        data[i] += part;
                }

			} catch(Exception e)
			{
				print (e.ToString());
			}
		}
	}



	void Update () 
	{

	}
}
