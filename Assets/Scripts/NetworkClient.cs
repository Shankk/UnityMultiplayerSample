using UnityEngine;
using Unity.Collections;
using Unity.Networking.Transport;
using NetworkMessages;
using NetworkObjects;
using System;
using System.Text;
using System.Collections.Generic;
using JetBrains.Annotations;

public class NetworkClient : MonoBehaviour
{
    public NetworkDriver m_Driver;
    public NetworkConnection m_Connection;
    public string serverIP;
    public ushort serverPort;


    public GameObject playerGO; // our player Object
    public string myAddress; // my address = (IP, PORT)
    public Dictionary<string, GameObject> currentPlayers; // A list of currently connected players
    public List<string> newPlayers, droppedPlayers; // a list of new players, and a list of dropped players
    public GameState lastestGameState; // the last game state received from server
    public ServerUpdateMsg initialSetofPlayers; // initial set of players to spawn

    void Start ()
    {
        // Initialize variables
        newPlayers = new List<string>();
        droppedPlayers = new List<string>();
        currentPlayers = new Dictionary<string, GameObject>();
        initialSetofPlayers = new ServerUpdateMsg();


        m_Driver = NetworkDriver.Create();
        m_Connection = default(NetworkConnection);
        var endpoint = NetworkEndPoint.Parse(serverIP,serverPort);
        m_Connection = m_Driver.Connect(endpoint);
    }
    
    void SendToServer(string message){
        var writer = m_Driver.BeginSend(m_Connection);
        NativeArray<byte> bytes = new NativeArray<byte>(Encoding.ASCII.GetBytes(message),Allocator.Temp);
        writer.WriteBytes(bytes);
        m_Driver.EndSend(writer);
    }

    void OnConnect(){
        Debug.Log("We are now connected to the server");

        //Example to send a handshake message:
         PlayerConnectedMsg m = new PlayerConnectedMsg();
         m.player.id = m_Connection.InternalId.ToString();
         SendToServer(JsonUtility.ToJson(m));
    }

    void SpawnPlayers()
    {
        if (newPlayers.Count > 0)
        {
            foreach (string playerID in newPlayers)
            {
                currentPlayers.Add(playerID, Instantiate(playerGO, new Vector3(0, 0, 0), Quaternion.identity));
                currentPlayers[playerID].name = playerID;
                if (playerID == myAddress)
                {
                    currentPlayers[playerID].AddComponent<PlayerController>();
                }
            }
            newPlayers.Clear();
        }
        if (initialSetofPlayers.players.Count > 0)
        {
            Debug.Log(initialSetofPlayers);
            foreach (NetworkObjects.NetworkPlayer player in initialSetofPlayers.players)
            {
                if (player.id == myAddress)
                    continue;
                currentPlayers.Add(player.id, Instantiate(playerGO, new Vector3(0, 0, 0), Quaternion.identity));
                currentPlayers[player.id].GetComponent<Renderer>().material.color = new Color(player.cubeColor.r, player.cubeColor.g, player.cubeColor.b);
                currentPlayers[player.id].name = player.id;

            }
            initialSetofPlayers.players = new List<NetworkObjects.NetworkPlayer>();
        }


    }

    void OnData(DataStreamReader stream){
        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length,Allocator.Temp);
        stream.ReadBytes(bytes);
        string recMsg = Encoding.ASCII.GetString(bytes.ToArray());
        NetworkHeader header = JsonUtility.FromJson<NetworkHeader>(recMsg);

        switch(header.cmd){
            case Commands.PLAYER_CONNECTED:
            PlayerConnectedMsg pcMsg = JsonUtility.FromJson<PlayerConnectedMsg>(recMsg);
            Debug.Log("Handshake message received!");
            Debug.Log("Our Internal ID: " + pcMsg.player.id);
            Debug.Log("Color R: " + pcMsg.player.cubeColor.r + " Color G: " + pcMsg.player.cubeColor.g + " Color B: " + pcMsg.player.cubeColor.b);
            Debug.Log("Pos X: " + pcMsg.player.cubPos.x + " Pos Y: " + pcMsg.player.cubPos.y + " Pos Z: " + pcMsg.player.cubPos.z);

            newPlayers.Add(pcMsg.player.id);
            myAddress = pcMsg.player.id;

            break;
            case Commands.PLAYER_UPDATE:
            PlayerUpdateMsg puMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
            Debug.Log("Player update message received!");
            break;
            case Commands.SERVER_UPDATE:
            ServerUpdateMsg suMsg = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);
            Debug.Log("Server update message received!");
            Debug.Log("PlayerList: " + suMsg.players[0].id);
            initialSetofPlayers = suMsg;
            
            //for (int i = 0; i < suMsg.players.Count; i++)
            //{
            //    initialSetofPlayers.players = new Player[i];
            //    initialSetofPlayers.players[i].id = suMsg.players[i].id;
            //}
                //for (int i = 0; i < suMsg.playerlist.players.Length; i++)
                //{
                //    initialSetofPlayers.players[0].id = suMsg.playerlist.players[0].id;
                //}
                break;
            default:
            Debug.Log("Unrecognized message received!");
            break;
        }
    }

    void Disconnect(){
        m_Connection.Disconnect(m_Driver);
        m_Connection = default(NetworkConnection);
        
    }

    void OnDisconnect(){
        Debug.Log("Client got disconnected from server");
        m_Connection = default(NetworkConnection);
    }

    public void OnDestroy()
    {
        m_Driver.Dispose();
    }   
    void Update()
    {
        m_Driver.ScheduleUpdate().Complete();

        if (!m_Connection.IsCreated)
        {
            return;
        }

        DataStreamReader stream;
        NetworkEvent.Type cmd;
        cmd = m_Connection.PopEvent(m_Driver, out stream);
        while (cmd != NetworkEvent.Type.Empty)
        {
            if (cmd == NetworkEvent.Type.Connect)
            {
                OnConnect();
            }
            else if (cmd == NetworkEvent.Type.Data)
            {
                OnData(stream);
            }
            else if (cmd == NetworkEvent.Type.Disconnect)
            {
                OnDisconnect();
            }

            cmd = m_Connection.PopEvent(m_Driver, out stream);
        }

        SpawnPlayers();
    }

    [Serializable]
    public struct receivedColor
    {
        public float R; public float G; public float B;
    }
    [Serializable]
    public struct receivedPosition
    {
        public float X; public float Y; public float Z;
    }
    [Serializable]
    public struct sentPosition
    {
        public float X; public float Y; public float Z;
    }

    [Serializable]
    public class Player
    {
        public string id;
        public receivedColor color;
        public receivedPosition pos;

    }

    [Serializable]
    public class ListOfPlayers
    {
        public Player[] players;

        public ListOfPlayers()
        {
            players = new Player[0];
        }
    }

    [Serializable]
    public class GameState
    {
        public int pktID;
        public Player[] players;
    }
}