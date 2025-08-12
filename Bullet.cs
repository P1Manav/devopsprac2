// Bullet.cs
using UnityEngine;
using System;
using System.Net.Sockets;
using System.Text;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class Bullet : MonoBehaviour
{
    [Header("Bullet Settings")]
    public float speed = 50f;
    public float lifeTime = 5f;

    // set at runtime by shooter
    [HideInInspector] public string ownerTag;   // "Player" or "Bot"
    [HideInInspector] public string shooterId;  // "player_1" or "bot_1"

    [Header("Server")]
    [HideInInspector] public string serverIP = "127.0.0.1";
    [HideInInspector] public int serverPort = 5000;

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null) Debug.LogError("[Bullet] Rigidbody required on bullet prefab.");
        else
        {
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }

        var col = GetComponent<Collider>();
        if (col == null) Debug.LogError("[Bullet] Collider required on bullet prefab.");
        else if (!col.isTrigger) Debug.LogWarning("[Bullet] Recommended: set Collider.IsTrigger = true on bullet prefab.");
    }

    void Start()
    {
        if (rb != null) rb.linearVelocity = transform.forward * speed;
        Destroy(gameObject, lifeTime);
    }

    void OnTriggerEnter(Collider other)
    {
        // ignore hitting the shooter
        if (!string.IsNullOrEmpty(ownerTag) && other.CompareTag(ownerTag))
            return;

        bool isPlayer = other.CompareTag("Player");
        bool isBot = other.CompareTag("Bot");

        string victimAgent = null;
        if (isBot)
        {
            var bc = other.GetComponent<BotController>();
            if (bc != null) victimAgent = bc.agentId;
        }
        else if (isPlayer)
        {
            victimAgent = "player_1";
        }

        // send rich hit report
        SendHitReport(shooterId, victimAgent, isPlayer ? "Player" : (isBot ? "Bot" : other.gameObject.tag));

        // optionally destroy hit object (gameplay choice)
        try { if (isPlayer || isBot) Destroy(other.gameObject); } catch { }

        Destroy(gameObject);
    }

    private void SendHitReport(string shooter, string victimAgent, string victimTag)
    {
        try
        {
            var payload = new HitMsg()
            {
                hit = true,
                shooter_id = shooter,
                victim_agent = victimAgent,
                victim_tag = victimTag
            };
            string json = JsonUtility.ToJson(payload) + "\n";
            using (TcpClient client = new TcpClient())
            {
                client.Connect(serverIP, serverPort);
                NetworkStream st = client.GetStream();
                byte[] data = Encoding.UTF8.GetBytes(json);
                st.Write(data, 0, data.Length);
                st.Flush();
                st.Close();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Bullet] Failed to send hit report: " + e.Message);
        }
    }

    [Serializable]
    private class HitMsg
    {
        public bool hit;
        public string shooter_id;
        public string victim_agent;
        public string victim_tag;
    }
}
