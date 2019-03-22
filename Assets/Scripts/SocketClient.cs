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
    // goals are the postions for that the agents
    // are currntly trying to get to
    public Vector2[] goals;
    // lightPositions stores the positions of all the
    // lights in the simualtion
    public Vector2[] lightPositions;
    //lightStates stores the current states of all of
    // the lights in the simulation
    public bool[] lightStates;
    // cameraPosition is the locatoin of the camera
    public float[] cameraPosition;
    // cameraDirection is the location the camera
    // points towards
    public float[] cameraDirection;
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

    // Model variables.
    public GameObject vehicle;
    public GameObject road;
    public Material roadM;
    public GameObject redLight;
    public GameObject greenLight;

    public Camera cam;

    // Start is called before the first frame update.
    void Start()
    {
        // Init the variables
        incomingQueue = new Queue<string>();
        path = Application.dataPath+ "/";

        // Load in the vehicle prefab
        //vehicle = (GameObject)Resources.Load("prefabs/vehicle", typeof(GameObject));

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
        while (true) { 
            try
            {
                // Connect to the server
                socketConnection = new TcpClient("localhost", 6666);
                Byte[] bytes = new Byte[8192];
                stream = socketConnection.GetStream();

                while (true)
                {
                    // Read incomming stream into byte arrary. 					
                    int length;
                    try
                    {
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
                    catch (SocketException socketException)
                    {
                        Debug.Log("Socket exception: " + socketException + " " + socketException.ErrorCode);

                    }
                }
            }
            catch (SocketException socketException)
            {
                Debug.Log("Socket exception: " + socketException + " " + socketException.ErrorCode);
                
            }
        }
    }

    // CreateScene populates the scene with vehicles based
    // upon the information sent from the server.
    void CreateScene(Message model)
    {
        // Create road system
        Debug.Log("Adding Roads");
        for (int i = 0; i < model.waypoints.Length-1; i++)
        {
            int j = i + 1;
            Vector3 posi = new Vector3(model.waypoints[i].x, 0f, model.waypoints[i].y);
            Vector3 posj = new Vector3(model.waypoints[j].x, 0f, model.waypoints[j].y);

            // Find the middle between the two points
            // middle is start plus half the diff
            // mid = start + (end-start)/2
            float x = model.waypoints[i].x + ((model.waypoints[j].x - model.waypoints[i].x) / 2);
            float z = model.waypoints[i].y + ((model.waypoints[j].y - model.waypoints[i].y) / 2);

            Vector3 pos = new Vector3(x, 0f, z);
            Quaternion rotation = new Quaternion();
            GameObject currentRoad = Instantiate(road, pos, rotation);

            // Calc and change the length of the road
            float len = Vector3.Distance(model.waypoints[j], model.waypoints[i]);
            currentRoad.transform.localScale = new Vector3(3f, 0.1f, len);

            // Rotate the road to look at the next waypoint
            currentRoad.transform.LookAt(posj);

        }

        // For every agent in the message spawn a vehicle object in
        // the position specified
        Debug.Log("Populating Scene");
        for (int i=0; i<model.agents.Length; i++)
        {
            // y becomes the z coordinate
            Vector3 pos = new Vector3(model.agents[i].x, 0f, model.agents[i].y);
            GameObject v = Instantiate(vehicle, pos, Quaternion.identity);
            // look at the waypoint they have to get to
            if (i < model.goals.Length)
            {
                v.transform.LookAt(new Vector3(model.goals[i].x, 0f, model.goals[i].y));
            }
        }
        Debug.Log("Finished populating scene");

        // Add the traffic lights to the scene
        Debug.Log("Added traffic Lights");
        if (model.lightPositions.Length == model.lightStates.Length) {
            for (int i=0; i<model.lightPositions.Length; i++)
            {
                // y becomes the z coordinate
                Vector3 pos = new Vector3(model.lightPositions[i].x, 0f, model.lightPositions[i].y);
                // true means a red light
                // false means a green light
                if (model.lightStates[i])
                {
                    Instantiate(redLight, pos, Quaternion.identity);
                }
                else
                {
                    Instantiate(greenLight, pos, Quaternion.identity);
                }
            }
        }

        // Update the camera position
        // If there is no specified position calc the position of
        // the camera
        if (model.cameraPosition.Length == 3)
        {
            cam.transform.position = new Vector3(model.cameraPosition[0], model.cameraPosition[1], model.cameraPosition[2]);
            cam.transform.LookAt(new Vector3(model.cameraDirection[0], model.cameraDirection[1], model.cameraDirection[2]));
        }
        else
        {
            // Calc the midpoint of the scene
            float x = 0;
            float z = 0;

            for (int i = 0; i < model.waypoints.Length - 1; i++)
            {
                x += model.waypoints[i].x;
                z += model.waypoints[i].y;
            }
            x /= model.waypoints.Length;
            z /= model.waypoints.Length;

            // set the camera position
            cam.transform.position = new Vector3(x,300,z);
            // Look stright down
            cam.transform.LookAt(new Vector3(x, 0, z));
        }

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
        // Generate random unique string
        const string glyphs = "abcdefghijklmnopqrstuvwxyz";
        int rndLength = 5;
        string randomStr = "";
        for (int i = 0; i < rndLength; i++)
        {
            randomStr += glyphs[UnityEngine.Random.Range(0, glyphs.Length)];
        }

        this.GetComponent<Camera>().Render();
        string filename = path + randomStr + tick + ".png";
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

        // Get all the GameObjects with tag "road"
        GameObject[] roads;
        roads = GameObject.FindGameObjectsWithTag("road");

        // Destroy the list of GameObjects
        foreach (GameObject road in roads)
        {
            Destroy(road);
        }

        // Get all the GameObjects with tag "light"
        GameObject[] lights;
        lights = GameObject.FindGameObjectsWithTag("light");

        // Destroy the list of GameObjects
        foreach (GameObject light in lights)
        {
            Destroy(light);
        }

        Debug.Log("Cleaned Up Scene");
    }
}
