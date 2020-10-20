using NetworkObjects;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace NetworkMessages
{
    public enum Commands{
        PLAYER_UPDATE,
        SERVER_UPDATE,
        PLAYER_CONNECTED,
        PLAYER_DISCONNECTED,
        CONNECTION_APPROVED,
        PLAYER_INPUT,
        GAMESTATE
    }

    [System.Serializable]
    public class NetworkHeader{
        public Commands cmd;
    }

    [System.Serializable]
    public class ConnectionApprovedMsg: NetworkHeader // SELF
    { 
        public List<NetworkObjects.NetworkPlayer> player;
        public ConnectionApprovedMsg(){      // Constructor
            cmd = Commands.CONNECTION_APPROVED;
            player = new List<NetworkObjects.NetworkPlayer>();
        }
    }

    [System.Serializable]
    public class PlayerConnectedMsg : NetworkHeader // EVERYONE ELSE
    {
        public List<NetworkObjects.NetworkPlayer> player;
        public PlayerConnectedMsg()
        {      // Constructor
            cmd = Commands.PLAYER_CONNECTED;
            player = new List<NetworkObjects.NetworkPlayer>();
        }
    }

    [System.Serializable]
    public class PlayerUpdateMsg : NetworkHeader
    {
        public NetworkObjects.NetworkPlayer player;
        public PlayerUpdateMsg()
        {      // Constructor
            cmd = Commands.PLAYER_UPDATE;
            player = new NetworkObjects.NetworkPlayer();
        }
    }

    [System.Serializable]
    public class PlayerDisconnectMsg : NetworkHeader
    {
        public NetworkObjects.NetworkPlayer player;
        public PlayerDisconnectMsg()
        {      // Constructor
            cmd = Commands.PLAYER_DISCONNECTED;
            player = new NetworkObjects.NetworkPlayer();
        }
    }

    [System.Serializable]
    public class GameStateMsg:NetworkHeader{
        public List<NetworkObjects.NetworkPlayer> GameState;
        public GameStateMsg(){      // Constructor
            cmd = Commands.GAMESTATE;
            GameState = new List<NetworkObjects.NetworkPlayer>();
        }
    };
    [System.Serializable]
    public class PlayerInputMsg:NetworkHeader{
        public Input myInput;
        public PlayerInputMsg(){
            cmd = Commands.PLAYER_INPUT;
            myInput = new Input();
        }
    }
    [System.Serializable]
    public class  ServerUpdateMsg:NetworkHeader{
        public List<NetworkObjects.NetworkPlayer> players;
        
        public ServerUpdateMsg(){      // Constructor
            cmd = Commands.SERVER_UPDATE;
            players = new List<NetworkObjects.NetworkPlayer>();
        }
    }
} 

namespace NetworkObjects
{
    //[System.Serializable]
    //public class NetworkObject{
    //    public string id;
    //}

    [System.Serializable]
    public class NetworkPlayer{
        public DateTime pulse;
        public string id;
        public Color cubeColor;
        public Vector3 cubPos;

        public NetworkPlayer(){
            cubeColor = new Color();
            cubPos = new Vector3();
        }
    }
}
