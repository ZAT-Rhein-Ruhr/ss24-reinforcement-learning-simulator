using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net.Sockets;
using System;
using System.Net;
using System.Text;
using System.IO;
using UnityEngine.SceneManagement;




public class SocketImageStreaming : MonoBehaviour
{

    private int port = 12374;
    private string ip = "0.0.0.0";

    private TcpServerClient client;
    private List<TcpServerClient> disconnectList;

    private TcpListener server;
    private bool serverStarted;
    public bool printLog = false;

    private TimeManagerKeyboard timeManager;
    // Start is called before the first frame update
    void Start()
    {

        disconnectList = new List<TcpServerClient>();
        try
        {
            server = new TcpListener(IPAddress.Any, port);
            server.Start();

            startListening();
            serverStarted = true;
            Log("System>>> Head image streaming server has been started on port " + port.ToString());
        }
        catch (Exception e)
        {
            Log("System>>> Socket Error:" + e.Message);
        }
    }

    private void Log(string text)
    {
        if(printLog)
        {
            Debug.Log(text);
        }

    }

    // Update is called once per frame
    void Update()
    {
        if (!serverStarted)
            return;
        if (client != null)
        {
            //Is the client still connected?
            if (!isConnected(client.tcp))
            {
                client.tcp.Close();
                disconnectList.Add(client);
            }
            else
            {
                NetworkStream s = client.tcp.GetStream();
                if (s.DataAvailable)
                {
                    
                    byte[] myReadBuffer = new byte[1024];
                    StringBuilder myCompleteMessage = new StringBuilder();
                    int numberOfBytesRead = 0;

                    // Incoming message may be larger than the buffer size.
                    do
                    {
                        numberOfBytesRead = s.Read(myReadBuffer, 0, myReadBuffer.Length);

                        myCompleteMessage.AppendFormat("{0}", Encoding.ASCII.GetString(myReadBuffer, 0, numberOfBytesRead));

                    }
                    while (s.DataAvailable);
                    string data = myCompleteMessage.ToString(); 
                    if (data != null && data != "")
                    {
                        
                        if(data.ToString().Contains("fetch")){
                            sendDataClient();
                        }
                        
                    }
                }       
                
            }

            //Check for message from the client
            for (int i = 0; i < disconnectList.Count - 1; i++)
            {

                disconnectList.RemoveAt(i);
            }
        }
    }

    private Texture2D renderImage(Camera camera)
    {
        // The Render Texture in RenderTexture.active is the one
        // that will be read by ReadPixels.
        var currentRT = RenderTexture.active;
        RenderTexture.active = camera.targetTexture;

        // Render the camera's view.
        camera.Render();

        // Make a new texture and read the active Render Texture into it.
        Texture2D image = new Texture2D(camera.targetTexture.width, camera.targetTexture.height);
        image.ReadPixels(new Rect(0, 0, camera.targetTexture.width, camera.targetTexture.height), 0, 0);
        image.Apply();

        // Replace the original active Render Texture.
        RenderTexture.active = currentRT;
        return image;
    }

    private Texture2D getImage()
    {
        Camera pepperHeadCamera = GameObject.Find("RGB Camera").GetComponent<Camera>();
        Texture2D image = renderImage(pepperHeadCamera);
        Debug.Log("Rendered image height: " + image.height + ", and width: " + image.width);
        return image;
    }


    private bool isConnected(TcpClient c)
    {
        try
        {
            if (c != null && c.Client != null && c.Client.Connected)
            {
                if (c.Client.Poll(0, SelectMode.SelectRead))
                {
                    return !(c.Client.Receive(new byte[1], SocketFlags.Peek) == 0);
                }
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }

    }

    private void startListening()
    {
        server.BeginAcceptTcpClient(acceptTcpClient, server);
    }

    private void acceptTcpClient(IAsyncResult ar)
    {
        TcpListener listener = (TcpListener)ar.AsyncState;

        client = new TcpServerClient(listener.EndAcceptTcpClient(ar));
        startListening();

        // Send a message to everyone, say somene has connected
        Log("System>>> Client connected.");
        //broadcast(clients[clients.Count-1].clientName + " has connected",clients);

        //broadcast("%NAME",new List<ServerClient>() { clients[clients.Count - 1] });
    }

    private void sendDataClient()
    {
        try
        {
            Texture2D image = getImage();
            PepperHeadImage response = new PepperHeadImage(image);
            string json = JsonUtility.ToJson(response);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            client.tcp.GetStream().Write(bytes, 0, bytes.Length);
        }
        catch (Exception e)
        {
            Log("System>>> Write error: " + e.Message + " to client " + client.clientName);
        }
    }

    void OnApplicationQuit()
    {
        try
        {
            client.Close();
        }
        catch(Exception e)
        {
            Debug.Log(e.Message);
        }

        // You must close the tcp listener
        try
        {
            server.Stop();
            serverStarted = false;
        }
        catch(Exception e)
        {
            Debug.Log(e.Message);
        }
    }

}

[Serializable]
public struct PepperHeadImage
{
    public string format;
    public byte[] image;

    public PepperHeadImage(Texture2D texture) {
        format = "png";
        image = texture.EncodeToPNG();
    }
}