using UnityEngine;
using UnityEngine.Assertions;
using Unity.Collections;
using Unity.Networking.Transport;
using NetworkMessages;
using System;
using System.Text;
using NetworkObjects;
using Unity.Mathematics;
using System.Collections.Generic;
using System.Linq;

public class NetworkServer : MonoBehaviour
{
    public NetworkDriver m_Driver;
    public ushort serverPort;
    private NativeList<NetworkConnection> m_Connections;
    public PlayerUpdateMsg playerData = new PlayerUpdateMsg();
    public GameStateMsg ConnectedList = new GameStateMsg();
    public GameStateMsg GameLoopData = new GameStateMsg();
    //public Dictionary<string, NetworkObjects.NetworkPlayer> player = new Dictionary<string, NetworkObjects.NetworkPlayer>(); // Holds Player Data

    void Start ()
    {
        m_Driver = NetworkDriver.Create();
        var endpoint = NetworkEndPoint.AnyIpv4;
        endpoint.Port = serverPort;
        if (m_Driver.Bind(endpoint) != 0)
            Debug.Log("Failed to bind to port " + serverPort);
        else
            m_Driver.Listen();

        m_Connections = new NativeList<NetworkConnection>(16, Allocator.Persistent);
    }

    void SendToClient(string message, NetworkConnection c){
        var writer = m_Driver.BeginSend(NetworkPipeline.Null, c);
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message),Allocator.Temp);
        writer.WriteBytes(bytes);
        m_Driver.EndSend(writer);
    }
    public void OnDestroy()
    {
        m_Driver.Dispose();
        m_Connections.Dispose();
    }

    void OnConnect(NetworkConnection c){
        

        m_Connections.Add(c);
        Debug.Log("Accepted a connection");
        //playerData.player.id = Null
        playerData.player.cubeColor.r = 0; playerData.player.cubeColor.g = 0; playerData.player.cubeColor.b = 0;
        playerData.player.cubPos.x = 0; playerData.player.cubPos.y = 0; playerData.player.cubPos.z = 0;

        Debug.Log("Connected List Length: " + ConnectedList.GameState.Count + " Connected User ID: " + c.InternalId);
       
        NetworkObjects.NetworkPlayer newConnectID = new NetworkObjects.NetworkPlayer();
        newConnectID.pulse = DateTime.Now;
        newConnectID.id = c.InternalId.ToString(); // Value 2
        newConnectID.cubeColor = playerData.player.cubeColor;
        newConnectID.cubPos = playerData.player.cubPos;

        ConnectedList.GameState.Add(newConnectID);
        
            
        SendToClient(JsonUtility.ToJson(ConnectedList), c);

        Debug.Log("Completed ConnectedList Update");

        // Send The Connected User Their own ID
        // Player 1 = 0, Player 2 = 1
        ConnectionApprovedMsg cpMsg = new ConnectionApprovedMsg();
        NetworkObjects.NetworkPlayer connectID = new NetworkObjects.NetworkPlayer();
        connectID.id = c.InternalId.ToString();
        cpMsg.player.Add(connectID);
        SendToClient(JsonUtility.ToJson(cpMsg), c);

        Debug.Log("Sent Connected User ID");

        // Sends The Player List To The Newly Connect ID
        ServerUpdateMsg suM = new ServerUpdateMsg();
        for (int i = 0; i < m_Connections.Length; i++) 
        {
            NetworkObjects.NetworkPlayer tempID = new NetworkObjects.NetworkPlayer();
            tempID.id = ConnectedList.GameState[i].id; 
            suM.players.Add(tempID);
            
            Debug.Log("Got This -> " + suM.players[i].id);
        }
        Debug.Log("Amount Connected -> " + m_Connections.Length);
        SendToClient(JsonUtility.ToJson(suM), c);

        // Send New Connected User ID to all Existing Players That Are Connected
        PlayerConnectedMsg pcMsg = new PlayerConnectedMsg();
        for (int i = 0; i < m_Connections.Length; i++)
        {
            if(c.InternalId.ToString() != ConnectedList.GameState[i].id) // 2 != 2    0, 1, will work
            {
                newConnectID.id = c.InternalId.ToString();
                pcMsg.player.Add(newConnectID);
                SendToClient(JsonUtility.ToJson(pcMsg), m_Connections[i]);
            }
        }
        
    }

    void GameLoop()
    {
        //Debug.Log("Running Game Loop...");
        Color randomColor = RandomColor();
        Color RandomColor()
        {
            return new Color(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value);
        }

        //Debug.Log("Amount In Server: " + m_Connections.Length);
        if(m_Connections.Length > 0)
        {
            for (int i = 0; i < m_Connections.Length; i++)
            {
                // Fill Game Loop Data
                GameLoopData.GameState.Insert(i, ConnectedList.GameState[i]);
            }
            for (int i = 0; i < m_Connections.Length; i++)
            {
                // Send Game Loop Data
                SendToClient(JsonUtility.ToJson(GameLoopData), m_Connections[i]);
            }
        }
        GameLoopData = new GameStateMsg();
    }
    void OnDisconnect(int i)
    {
        Debug.Log("Client disconnected from server, Player ID: " + i);
        
        // Send Most Recently Disconnected User ID to all Connected Users
        PlayerDisconnectMsg pdMsg = new PlayerDisconnectMsg();
        for (int dcID = 0; dcID < m_Connections.Length; dcID++)
        {
            if (i.ToString() != ConnectedList.GameState[dcID].id)
            {
                pdMsg.player.id = i.ToString();
                SendToClient(JsonUtility.ToJson(pdMsg), m_Connections[dcID]);
            }
        }

        ConnectedList.GameState.RemoveAt(i);
        m_Connections[i] = default(NetworkConnection);
    }

    void OnData(DataStreamReader stream, int i){
        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length,Allocator.Temp);
        stream.ReadBytes(bytes);
        string recMsg = Encoding.ASCII.GetString(bytes.ToArray());
        NetworkHeader header = JsonUtility.FromJson<NetworkHeader>(recMsg);

        switch(header.cmd){
            case Commands.PLAYER_CONNECTED:
            ConnectionApprovedMsg cpMsg = JsonUtility.FromJson<ConnectionApprovedMsg>(recMsg);
            Debug.Log("Handshake message received!");
            
            break;
            case Commands.PLAYER_UPDATE:
            PlayerUpdateMsg puMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
            //Debug.Log("Player update message received!");
            playerData = puMsg;
            Debug.Log("Player ID: " + playerData.player.id  + " Player Position: " + playerData.player.cubPos);
            //Debug.Log("Time As of Now: " + DateTime.Now);
            playerData.player.pulse = DateTime.Now;
            for(i = 0; i < ConnectedList.GameState.Count; i++)
            {
                if(playerData.player.id  == ConnectedList.GameState[i].id)
                {
                    ConnectedList.GameState.Insert(i, playerData.player);
                    ConnectedList.GameState.RemoveAt(i + 1);
                }
                Debug.Log("Transfered ID: " + ConnectedList.GameState[i].id + "Transfered Pos: " + ConnectedList.GameState[i].cubPos);
            }
            

            break;
            case Commands.PLAYER_DISCONNECTED:
            PlayerDisconnectMsg pdMsg = JsonUtility.FromJson<PlayerDisconnectMsg>(recMsg);
            Debug.Log("Player Disconnect update message received!, Got This: " + i);
            OnDisconnect(i);
            break;
            case Commands.GAMESTATE:
            GameStateMsg gsMsg = JsonUtility.FromJson<GameStateMsg>(recMsg);
            Debug.Log("GameState update message received!");
            break;
            case Commands.SERVER_UPDATE:
            ServerUpdateMsg suMsg = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);
            Debug.Log("Server update message received!");
            break;
            default:
            Debug.Log("SERVER ERROR: Unrecognized message received!");
            break;
        }
    }

    
    void Update ()
    {
        m_Driver.ScheduleUpdate().Complete();

       
        // AcceptNewConnections
        NetworkConnection c = m_Driver.Accept();
        while (c  != default(NetworkConnection))
        {            
            OnConnect(c);

            // Check if there is another new connection
            c = m_Driver.Accept();
        }

        // CleanUpConnections
        for (int i = 0; i < m_Connections.Length; i++)
        {
            if (!m_Connections[i].IsCreated)
            {

                m_Connections.RemoveAtSwapBack(i);
                --i;
            }
        }

        GameLoop();

        // Read Incoming Messages
        DataStreamReader stream;
        for (int i = 0; i < m_Connections.Length; i++)
        {
            Assert.IsTrue(m_Connections[i].IsCreated);
            
            NetworkEvent.Type cmd;
            cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream);
            while (cmd != NetworkEvent.Type.Empty)
            {
                if (cmd == NetworkEvent.Type.Data)
                {
                    OnData(stream, i);
                }
                else if (cmd == NetworkEvent.Type.Disconnect)
                {
                    OnDisconnect(i);
                }

                cmd = m_Driver.PopEventForConnection(m_Connections[i], out stream);
            }
            //Debug.Log("Players Time: " + ConnectedList.GameState[i].pulse + " Server Time: " + DateTime.Now +" Player Time In Seconds: " + ConnectedList.GameState[i].pulse.Second);
            //Debug.Log("Time Difference: " + (DateTime.Now - ConnectedList.GameState[i].pulse) + " Time Span Amount: " + TimeSpan.FromSeconds(5));
            if ((DateTime.Now - ConnectedList.GameState[i].pulse) > TimeSpan.FromSeconds(5))
            {
                OnDisconnect(i);
            }
        }

        
    }
}