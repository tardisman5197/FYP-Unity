using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;


// Message stores the information sent by the server.
public class Message
{
    // agents contain a list of the coordinates of each
    // agent in the scene
    public Vector2[] agents;
    // waypoints contain the coordinates that agents
    // can travel between
    public Vector2[] waypoints;
    // tick represents the time at which the agents
    // are in the given positions.
    public int tick;

}

// Receipt is an object representation of the information
// that is the response to a server request.
public class Receipt
{
    // filepath stores the location of the screenshot
    public string filepath;
}

public class SocketClient : MonoBehaviour
{
    // Socket variables
    // TcpClient is the connection to the server
    private TcpClient socketConnection;
    // clientReceiveThread is the thread which messages from the
    // server are read on
    private Thread clientReceiveThread;
    // stream is the NetworkStream which the server can be written
    // to and messages from the server can be read from
    private NetworkStream stream;
    // incomingQueue stores the messages sent from the server
    private Queue<string> incomingQueue;
    // path contains the file path to where screenshots can be saved
    private string path;

    // Model variables
    // vehicle contains the prefab to create an instance of
    // a vehicle prefab.
    private GameObject vehicle;

    // Start is called before the first frame update.
    void Start()
    {
        // Init the variables
        incomingQueue = new Queue<string>();
        path = Application.dataPath + "/screenshots/";

        // Load in the vehicle prefab
        vehicle = (GameObject)Resources.Load("prefabs/vehicle", typeof(GameObject));

        // Connect to the server
        ConnectToTcpServer();
    }

    // Update is called once per frame.
    void Update()
    {
        // Check if a messages has been sent by the server
        if (incomingQueue.Count > 0)
        {
            // Destroy any objects from previous scenes
            CleanUpScene();

            // Convert msg into an object
            string msg = incomingQueue.Dequeue();
            Debug.Log("server message received as: " + msg);
            Message model = JsonUtility.FromJson<Message>(msg);

            // Populate the scene and take screenshot
            CreateScene(model);
            SendReceipt(TakeScreenshot(path, model.tick));
        }
        
    }

    // ConnectToTcpServer creates a thread for ListenForData.
    private void ConnectToTcpServer()
    {
        try
        {
            // Connect to the server and start listening for messages
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

    // ListenForData inits the socketConnection then listens for data from the server.
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
                // Read incomming stream into byte arrary. 					
                int length;
                while ((length = stream.Read(bytes, 0, bytes.Length)) != 0)
                {
                    // Read the message and convert into a string 						
                    var incommingData = new byte[length];
                    Array.Copy(bytes, 0, incommingData, 0, length);
                    string serverMessage = Encoding.ASCII.GetString(incommingData);

                    // Add the data to be acted upon
                    incomingQueue.Enqueue(serverMessage);
                }
            }      
        }
        catch (SocketException socketException)
        {
            Debug.Log("Socket exception: " + socketException);
        }
    }

    // CreateScene populates the scene with vehicles based
    // upon the information sent from the server.
    void CreateScene(Message model)
    {
        Debug.Log("Populating Scene");
        // For every agent in the message spawn a vehicle object in
        // the position specified
        for (int i=0; i<model.agents.Length; i++)
        {
            Vector3 pos = new Vector3(model.agents[i].x, model.agents[i].y);
            Instantiate(vehicle, pos, Quaternion.identity);
        }

        Debug.Log("Finished populating scene");

    }

    // SendReceipt takes in a filename and sends the server
    // information about the screenshot that had just been taken.
    void SendReceipt(string filename)
    {
        // Create recipt object and set values
        Receipt r = new Receipt
        {
            filepath = filename
        };

        // Convert recipt into a json string
        string jsonstr = JsonUtility.ToJson(r);
        byte[] bytesToSend = ASCIIEncoding.ASCII.GetBytes(jsonstr);

        // Send the recipt to the server
        Console.WriteLine("Sending Receipt");
        stream.Write(bytesToSend, 0, bytesToSend.Length);
    }

    // TakeScreenshot takes in a filepath and simulation tick then
    // takes a screenshot and saves the outcome as a png in the
    // given filepath. Then retruns the filename of the image.
    string TakeScreenshot(string path, int tick)
    {
        string filename = path + tick + ".png";
        ScreenCapture.CaptureScreenshot(filename);
        return filename;
    }

    // CleanUpScene destroyes any objects in the scene.
    void CleanUpScene()
    {
        // Get all the GameObjects with tag "vehicle"
        GameObject[] vehicles;
        vehicles = GameObject.FindGameObjectsWithTag("vehicle");

        // Destroy the list of GameObjects
        foreach (GameObject vehicle in vehicles)
        {
            Destroy(vehicle);
        }

        Debug.Log("Cleaned Up Scene");
    }
}
