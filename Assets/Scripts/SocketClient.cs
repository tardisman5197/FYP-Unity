using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class message
{
    public Vector2[] agents;
    public Vector2[] waypoints;
    public int tick;

}

public class receipt
{
    public string filepath;
}

public class SocketClient : MonoBehaviour
{
    // Socket variables
    private TcpClient socketConnection;
    private Thread clientReceiveThread;
    private NetworkStream stream;
    private string path;

    // Model variables
    private GameObject vehicle;
    private Queue<string> incomingQueue;

    // Start is called before the first frame update
    void Start()
    {
        incomingQueue = new Queue<string>();
        path = Application.dataPath + "/screenshots/";
        ConnectToTcpServer();
    }

    // Update is called once per frame
    void Update()
    {
        if (incomingQueue.Count > 0)
        {
            cleanUpScene();
            string msg = incomingQueue.Dequeue();
            Debug.Log("server message received as: " + msg);
            message model = JsonUtility.FromJson<message>(msg);
            createScene(model);
            sendReceipt(takeScreenshot(path, model.tick));
        }
        
    }

    // ConnectToTcpServer creates a thread for ListenForData
    private void ConnectToTcpServer()
    {
        try
        {
            Debug.Log("Connecting to Server");
            clientReceiveThread = new Thread(new ThreadStart(ListenForData));
            clientReceiveThread.IsBackground = true;
            clientReceiveThread.Start();
        }
        catch (Exception e)
        {
            Debug.Log("On client connect exception " + e);
        }
    }

    // ListenForData inits the socketConnection then listens for data from the server
    private void ListenForData()
    {
        try
        {
            // Connect to the server
            socketConnection = new TcpClient("localhost", 6666);
            Byte[] bytes = new Byte[1024];

            stream = socketConnection.GetStream();
            while (true)
            {
                // Get a stream object for reading 				
                int length;
                // Read incomming stream into byte arrary. 					
                while ((length = stream.Read(bytes, 0, bytes.Length)) != 0)
                {
                    var incommingData = new byte[length];
                    Array.Copy(bytes, 0, incommingData, 0, length);
                    // Convert byte array to string message. 						
                    string serverMessage = Encoding.ASCII.GetString(incommingData);
                    incomingQueue.Enqueue(serverMessage);
                }

            }
            
        }
        catch (SocketException socketException)
        {
            Debug.Log("Socket exception: " + socketException);
        }
    }

    void createScene(message model)
    {
        vehicle = (GameObject)Resources.Load("prefabs/vehicle", typeof(GameObject));
        Debug.Log("Creating Vehicles");
        for (int i=0; i<model.agents.Length; i++)
        {
            Vector3 pos = new Vector3(model.agents[i].x, model.agents[i].y);
            Instantiate(vehicle, pos, Quaternion.identity);
        }

        Debug.Log("Done creating model");

    }

    void sendReceipt(string filename)
    {
        receipt r = new receipt();
        r.filepath = filename;
        string jsonstr = JsonUtility.ToJson(r);
        byte[] bytesToSend = ASCIIEncoding.ASCII.GetBytes(jsonstr);
        Console.WriteLine("Sending");
        stream.Write(bytesToSend, 0, bytesToSend.Length);
    }

    string takeScreenshot(string path, int tick)
    {
        string filename = path + tick + ".png";
        ScreenCapture.CaptureScreenshot(filename);
        return filename;
    }

    void cleanUpScene()
    {
        GameObject[] vehicles;

        vehicles = GameObject.FindGameObjectsWithTag("vehicle");

        foreach (GameObject vehicle in vehicles)
        {
            Destroy(vehicle);
        }

        Debug.Log("Cleaned Up Scene");
    }
}
