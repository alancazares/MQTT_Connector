using System;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Options;
using MQTTnet.Protocol;

public class MyEvent : UnityEvent<string> { }

public class MQTT : MonoBehaviour
{
    private static IMqttClient client;
    private static IMqttClientOptions clientOptions;
    public bool isConnected = false;

    [Header("MQTT Settings")]
    public string broker = "";
    public int brokerport = 8883;
    public string clientid = "UnityClient_" + Guid.NewGuid().ToString("N").Substring(0, 8);
    public string username = "";
    public string password = "";

    public MyEvent TestEvent;

    // Events
    public delegate void ReceivedMessage(string receivedText);
    public static event ReceivedMessage OnMessage;

    public delegate void SentMessage(string sentText, string topic, int qos);
    public static event SentMessage OnSentMessage;

    public delegate void Connected();
    public static event Connected OnConnect;

    public delegate void Disconnected();
    public static event Disconnected OnDisconnect;

    void Awake()
    {
        TestEvent = new MyEvent();
    }

    void Start()
    {
        if (string.IsNullOrWhiteSpace(clientid))
            clientid = "UnityClient_" + Guid.NewGuid().ToString("N").Substring(0, 8);

        Init();
        _ = Connect();
    }

    void Update()
    {
        if (client != null)
        {
            isConnected = client.IsConnected;
        }
    }

    private void Init()
    {
        var factory = new MqttFactory();
        client = factory.CreateMqttClient();

        clientOptions = new MqttClientOptionsBuilder()
            .WithClientId(clientid)
            .WithTcpServer(broker, brokerport)
            .WithCredentials(username, password)
            .WithCleanSession(true)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(60))
            .Build();

        client.UseApplicationMessageReceivedHandler(e =>
        {
            string payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
            Debug.Log($"[MQTT] Message received: {payload}");

            OnMessage?.Invoke(payload);
            TestEvent?.Invoke(payload);
        });

        client.UseConnectedHandler(async e =>
        {
            Debug.Log("[MQTT] Connected to broker.");
            OnConnect?.Invoke();
            TestEvent?.Invoke("MQTT Connected");

            await client.SubscribeAsync(new MqttTopicFilterBuilder().WithTopic("#").Build());
            Debug.Log("[MQTT] Subscribed to all topics.");
        });

        client.UseDisconnectedHandler(e =>
        {
            Debug.LogWarning("[MQTT] Disconnected from broker.");
            OnDisconnect?.Invoke();
        });
    }

    public async Task Connect()
    {
        try
        {
            Debug.Log("[MQTT] Connecting...");
            await client.ConnectAsync(clientOptions);
        }
        catch (Exception ex)
        {
            Debug.LogError("[MQTT] Connection failed:\n" + ex.Message);
        }
    }

    public async void Disconnect()
    {
        if (client != null && client.IsConnected)
        {
            await client.DisconnectAsync();
            Debug.Log("[MQTT] Disconnected.");
        }
    }

    public async void SendMessage(string text, string topic, int qos)
    {
        if (client == null || !client.IsConnected)
        {
            Debug.LogWarning("[MQTT] Cannot send message, client not connected.");
            return;
        }

        var builder = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(text);

        switch (qos)
        {
            case 0: builder.WithAtMostOnceQoS(); break;
            case 1: builder.WithAtLeastOnceQoS(); break;
            case 2: builder.WithExactlyOnceQoS(); break;
            default: builder.WithAtMostOnceQoS(); break;
        }

        var message = builder.Build();
        await client.PublishAsync(message);

        Debug.Log($"[MQTT] Published: {text} to topic: {topic} (QoS {qos})");
        OnSentMessage?.Invoke(text, topic, qos);
    }
    private async void OnApplicationQuit()
    {
        await DisconnectClient();
    }

    private async void OnDisable()
    {
        await DisconnectClient();
    }

    private async Task DisconnectClient()
    {
        if (client != null && client.IsConnected)
        {
            Debug.Log("[MQTT] Disconnecting on quit/disable...");
            await client.DisconnectAsync();
            client.Dispose();
            client = null;
        }
    }



}
