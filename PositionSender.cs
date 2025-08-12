using UnityEngine;
using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

public class PositionSender : MonoBehaviour
{
    public Transform player;
    public Transform botTransform;
    public BotController botController;
    public string agentId = "bot_1";
    public string serverIP = "127.0.0.1";
    public int serverPort = 5000;
    public float sendInterval = 0.05f; // ~20 FPS update

    private Thread netThread;
    private volatile bool running = false;
    private TcpClient client;
    private NetworkStream stream;
    private object lockObj = new object();

    private string pendingStateJson = null;
    private Queue<string> incomingResponses = new Queue<string>();
    private string recvBuffer = "";

    private static bool alreadyExists = false; // singleton protection

    void Start()
    {
        if (alreadyExists)
        {
            Debug.LogError("[PositionSender] Duplicate instance detected. Destroying...");
            Destroy(gameObject);
            return;
        }
        alreadyExists = true;

        if (player == null || botTransform == null || botController == null)
        {
            Debug.LogError("[PositionSender] Assign player, botTransform and botController in Inspector!");
            enabled = false;
            return;
        }

        running = true;
        netThread = new Thread(NetLoop) { IsBackground = true };
        netThread.Start();
    }

    void Update()
    {
        // build state
        StateData sd = new StateData()
        {
            agent_id = agentId,
            player_pos = new float[] { player.position.x, player.position.y, player.position.z },
            player_vel = GetVel(player),
            player_rot = new float[] { player.eulerAngles.x, player.eulerAngles.y, player.eulerAngles.z },

            bot_pos = new float[] { botTransform.position.x, botTransform.position.y, botTransform.position.z },
            bot_vel = GetVel(botTransform),
            bot_rot = new float[] { botTransform.eulerAngles.x, botTransform.eulerAngles.y, botTransform.eulerAngles.z }
        };

        string json = JsonUtility.ToJson(sd) + "\n";
        lock (lockObj)
        {
            pendingStateJson = json;
        }

        // apply incoming predictions
        lock (lockObj)
        {
            while (incomingResponses.Count > 0)
            {
                string line = incomingResponses.Dequeue();
                try
                {
                    Prediction p = JsonUtility.FromJson<Prediction>(line);
                    Debug.LogFormat("[PositionSender] Prediction: yaw={0}, pitch={1}, roll={2}, shoot={3}",
                        p.yaw_delta, p.pitch_delta, p.roll_delta, p.shoot);
                    botController.SetPrediction(p.yaw_delta, p.pitch_delta, p.roll_delta, p.shoot);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[PositionSender] Parse fail: " + e.Message + " raw:" + line);
                }
            }
        }
    }

    void OnApplicationQuit()
    {
        running = false;
        try { stream?.Close(); client?.Close(); } catch { }
    }

    float[] GetVel(Transform t)
    {
        var rb = t.GetComponent<Rigidbody>();
        if (rb != null)
            return new float[] { rb.linearVelocity.x, rb.linearVelocity.y, rb.linearVelocity.z };
        return new float[] { 0f, 0f, 0f };
    }

    private void NetLoop()
    {
        while (running)
        {
            try
            {
                if (client == null || !client.Connected)
                {
                    Debug.Log("[PositionSender] Connecting to " + serverIP + ":" + serverPort);
                    client = new TcpClient();
                    client.NoDelay = true;
                    client.Connect(serverIP, serverPort);
                    stream = client.GetStream();
                    stream.ReadTimeout = 2000;
                    Debug.Log("[PositionSender] Connected.");
                }

                // send state
                string toSend = null;
                lock (lockObj) { toSend = pendingStateJson; pendingStateJson = null; }
                if (!string.IsNullOrEmpty(toSend))
                {
                    byte[] data = Encoding.UTF8.GetBytes(toSend);
                    stream.Write(data, 0, data.Length);
                    stream.Flush();
                }

                // receive responses
                while (client.Available > 0)
                {
                    byte[] buf = new byte[8192];
                    int read = stream.Read(buf, 0, buf.Length);
                    if (read <= 0) throw new Exception("Server closed connection");
                    recvBuffer += Encoding.UTF8.GetString(buf, 0, read);

                    while (recvBuffer.Contains("\n"))
                    {
                        int idx = recvBuffer.IndexOf("\n");
                        string line = recvBuffer.Substring(0, idx).Trim();
                        recvBuffer = recvBuffer.Substring(idx + 1);
                        if (!string.IsNullOrEmpty(line))
                        {
                            lock (lockObj) incomingResponses.Enqueue(line);
                        }
                    }
                }

                Thread.Sleep((int)(sendInterval * 1000));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PositionSender] Connection lost: " + e.Message);
                try { stream?.Close(); client?.Close(); } catch { }
                Thread.Sleep(1000); // wait before reconnect
            }
        }
    }

    [Serializable]
    public class StateData
    {
        public string agent_id;
        public float[] player_pos;
        public float[] player_vel;
        public float[] player_rot;
        public float[] bot_pos;
        public float[] bot_vel;
        public float[] bot_rot;
    }

    [Serializable]
    public class Prediction
    {
        public float yaw_delta;
        public float pitch_delta;
        public float roll_delta;
        public bool shoot;
    }
}
