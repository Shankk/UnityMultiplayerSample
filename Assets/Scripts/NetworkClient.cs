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
    public GameStateMsg lastestGameState; // the last game state received from server
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
         //ConnectionApprovedMsg m = new ConnectionApprovedMsg();
         //m.player.id = m_Connection.InternalId.ToString();
         //SendToServer(JsonUtility.ToJson(m));
    }

    void SpawnPlayers()
    {
        if (newPlayers.Count > 0)
        {
            foreach (string playerID in newPlayers)
            {
                currentPlayers.Add(playerID, Instantiate(playerGO, new Vector3(0, 0, 0), Quaternion.identity));
                currentPlayers[playerID].name = playerID;
                Debug.Log("Recieved: " + playerID + "My Address: " + myAddress);
                if (playerID == myAddress)
                {
                    currentPlayers[playerID].AddComponent<PlayerController>();
                }
            }
            newPlayers.Clear();
        }
        if (initialSetofPlayers.players.Count > 0)
        {
           
            foreach (NetworkObjects.NetworkPlayer player in initialSetofPlayers.players)
            {
                Debug.Log("Current Players: " + player.id + " Our InternalID: " + myAddress);
                if (player.id == myAddress)
                    continue;
               
                currentPlayers.Add(player.id, Instantiate(playerGO, new Vector3(0, 0, 0), Quaternion.identity));
                currentPlayers[player.id].GetComponent<Renderer>().material.color = new Color(player.cubeColor.r, player.cubeColor.g, player.cubeColor.b);
                currentPlayers[player.id].name = player.id;
                
            }
            initialSetofPlayers.players = new List<NetworkObjects.NetworkPlayer>();
        }
    }

    void UpdatePlayers()
    {
        if (lastestGameState.GameState.Count > 0)
        {
            foreach (NetworkObjects.NetworkPlayer player in lastestGameState.GameState)
            {
                string playerID = player.id;
                Debug.Log("Game State ID: " + player.id + " Our InternalID: " + myAddress);
                //currentPlayers[player.id].GetComponent<Renderer>().material.color = new Color(player.cubeColor.r, player.cubeColor.g, player.cubeColor.b);
                if (player.id != myAddress)
                {
                    Debug.Log("Setting Position of random ass player");
                    currentPlayers[player.id].GetComponent<Transform>().position = new Vector3(player.cubPos.x, player.cubPos.y, player.cubPos.z);
                }
            }
            foreach (NetworkObjects.NetworkPlayer player in lastestGameState.GameState)
            {
                if (player.id == myAddress)
                {
                    Debug.Log("Yeeting my position to the server");
                    PlayerUpdateMsg playerData = new PlayerUpdateMsg();
                    //playerData.player.pulse = DateTime.Now;
                    playerData.player.id = player.id;
                    playerData.player.cubPos.x = currentPlayers[player.id].GetComponent<Transform>().position.x;
                    playerData.player.cubPos.y = currentPlayers[player.id].GetComponent<Transform>().position.y;
                    playerData.player.cubPos.z = currentPlayers[player.id].GetComponent<Transform>().position.z;
                    SendToServer(JsonUtility.ToJson(playerData));

                }
            }
            lastestGameState.GameState = new List<NetworkObjects.NetworkPlayer>();
        }
    }

    void DestroyPlayers()
    {
        if (droppedPlayers.Count > 0)
        {
            foreach (string playerID in droppedPlayers)
            {
                //Debug.Log(playerID);
                //Debug.Log(currentPlayers[playerID]);
                Destroy(currentPlayers[playerID].gameObject);
                currentPlayers.Remove(playerID);
            }
            droppedPlayers.Clear();
        }
    }

    void OnData(DataStreamReader stream){
        NativeArray<byte> bytes = new NativeArray<byte>(stream.Length,Allocator.Temp);
        stream.ReadBytes(bytes);
        string recMsg = Encoding.ASCII.GetString(bytes.ToArray());
        NetworkHeader header = JsonUtility.FromJson<NetworkHeader>(recMsg);

        switch(header.cmd){
            case Commands.CONNECTION_APPROVED: // SELF
            ConnectionApprovedMsg cpMsg = JsonUtility.FromJson<ConnectionApprovedMsg>(recMsg);
            Debug.Log("Connection Approved message received!");
           
            foreach(NetworkObjects.NetworkPlayer player in cpMsg.player)
            {
                Debug.Log("Our Internal ID: " + player.id);
                Debug.Log("Color R: " + player.cubeColor.r + " Color G: " + player.cubeColor.g + " Color B: " + player.cubeColor.b);
                Debug.Log("Pos X: " + player.cubPos.x + " Pos Y: " + player.cubPos.y + " Pos Z: " + player.cubPos.z);

                newPlayers.Add(player.id);
                myAddress = player.id;
            }
            break;
            case Commands.PLAYER_CONNECTED: // Everyone else
            PlayerConnectedMsg pcMsg = JsonUtility.FromJson<PlayerConnectedMsg>(recMsg);
            Debug.Log("Player Connected message received!");

            foreach (NetworkObjects.NetworkPlayer player in pcMsg.player)
            {
                //Debug.Log("Our Internal ID: " + player.id);
                //Debug.Log("Color R: " + player.cubeColor.r + " Color G: " + player.cubeColor.g + " Color B: " + player.cubeColor.b);
                //Debug.Log("Pos X: " + player.cubPos.x + " Pos Y: " + player.cubPos.y + " Pos Z: " + player.cubPos.z);

                newPlayers.Add(player.id);
            }
            break;
            case Commands.PLAYER_DISCONNECTED:
            PlayerDisconnectMsg pdMsg = JsonUtility.FromJson<PlayerDisconnectMsg>(recMsg);
            Debug.Log("Player disconnect message received! User ID: " + pdMsg.player.id);
            droppedPlayers.Add(pdMsg.player.id);
            break;
            case Commands.PLAYER_UPDATE:
            PlayerUpdateMsg puMsg = JsonUtility.FromJson<PlayerUpdateMsg>(recMsg);
            Debug.Log("Player update message received!");
            break;
            case Commands.GAMESTATE:
            GameStateMsg gsMsg = JsonUtility.FromJson<GameStateMsg>(recMsg);
            Debug.Log("Game State update message received!");
            lastestGameState = gsMsg;
            for(int i = 0; i < lastestGameState.GameState.Count; i++)
            {
                Debug.Log("Game State ID: " + lastestGameState.GameState[i].id + " RGB: " + lastestGameState.GameState[i].cubeColor + "Pos: " + lastestGameState.GameState[i].cubPos);
            }
            
            
            break;
            case Commands.SERVER_UPDATE: // Gives The Player List Already In Server
            ServerUpdateMsg suMsg = JsonUtility.FromJson<ServerUpdateMsg>(recMsg);
            Debug.Log("Server update message received!");
            for(int i = 0; i < suMsg.players.Count; i++)
            {
                Debug.Log("PlayerList: " + suMsg.players[i].id);
            }
            
            initialSetofPlayers = suMsg;
            
            break;
            default:
            Debug.Log("Unrecognized message received!");
            break;
        }
    }
    void Disconnect()
    {
        PlayerDisconnectMsg pdMsg = new PlayerDisconnectMsg();
        SendToServer(JsonUtility.ToJson(pdMsg));

        m_Connection.Disconnect(m_Driver);
        m_Connection = default(NetworkConnection);
        Debug.Log("We got disconnected from server");
      
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
        UpdatePlayers();
        DestroyPlayers();
    }
}