using System.Collections.Generic;
using UnityEngine;
using Riptide;


public class ClientInput
{
    public bool[] Inputs = new bool[6];
    public ushort currentTick = 0;
    public Quaternion rotation;
}

public class Player : MonoBehaviour
{
    public static Dictionary<ushort, Player> List { get; private set; } = new Dictionary<ushort, Player>();

    public ushort Id { get; private set; }
    public string Username { get; private set; }

    private ClientInput lastReceivedInputs = new ClientInput();

    private Vector3 playerVelocity;
    private Vector3 playerAngularVelocity;

    public PlayerMovement playerMovement;

        

    private void Start()
    {
        Physics.simulationMode = SimulationMode.Script;
            
    }

    private void HandleClientInput(ClientInput[] inputs, ushort clientID)
    {
        if (inputs.Length == 0) return;

        if (inputs[inputs.Length - 1].currentTick >= lastReceivedInputs.currentTick)
        {
            int start = lastReceivedInputs.currentTick > inputs[0].currentTick ? (lastReceivedInputs.currentTick - inputs[0].currentTick) : 0;

            //I need to create a new object to store all the players velocitys
            //Do the phsyics
            //then renable them with the proper 
            foreach (KeyValuePair<ushort, Player> entry in List)
            {
                ushort playerId = entry.Key;
                Player player = entry.Value;
                if (playerId != clientID)
                {
                    player.playerVelocity = player.playerMovement.rb.velocity;
                    player.playerAngularVelocity = player.playerMovement.rb.angularVelocity;
                    player.playerMovement.rb.isKinematic = true;
                }
                else
                    player.playerMovement.rb.isKinematic = false;
            }
            for (int i = start; i < inputs.Length - 1; i++)
            {
                playerMovement.PhysicsStep(inputs[i].Inputs, inputs[i].rotation);
                Physics.Simulate(NetworkManager.Singleton.TickRate);
                SendMovement((ushort)(inputs[i].currentTick + 1));
            }

            foreach (KeyValuePair<ushort, Player> entry in List)
            {
                Player player = entry.Value;
                ushort playerId = entry.Key;
                player.playerMovement.rb.isKinematic = false;
                if (playerId != clientID)
                {
                    player.playerMovement.rb.velocity = player.playerVelocity;
                    player.playerMovement.rb.angularVelocity = player.playerAngularVelocity;
                }



            }
            lastReceivedInputs = inputs[inputs.Length - 1];
        }
    }


    private void OnDestroy()
    {
        List.Remove(Id);
    }

    public static void Spawn(ushort id, string username)
    {
        Player player = Instantiate(NetworkManager.Singleton.PlayerPrefab, new Vector3(0f, 1f, 0f), Quaternion.identity).GetComponent<Player>();
        player.name = $"Player {id} ({(username == "" ? "Guest" : username)})";
        player.Id = id;
        player.Username = username;

        player.SendSpawn();
        List.Add(player.Id, player);
    }

    #region Messages
    /// <summary>Sends a player's info to the given client.</summary>
    /// <param name="toClient">The client to send the message to.</param>
    public void SendSpawn(ushort toClient)
    {
        NetworkManager.Singleton.Server.Send(GetSpawnData(Message.Create(MessageSendMode.Reliable, ServerToClientId.SpawnPlayer)), toClient);
    }
    /// <summary>Sends a player's info to all clients.</summary>
    private void SendSpawn()
    {
        NetworkManager.Singleton.Server.SendToAll(GetSpawnData(Message.Create(MessageSendMode.Reliable, ServerToClientId.SpawnPlayer)));
    }

    private Message GetSpawnData(Message message)
    {
        message.AddUShort(Id);
        message.AddString(Username);
        message.AddVector3(transform.position);
        return message;
    }

    private void SendMovement(ushort clientTick)
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ServerToClientId.PlayerMovement);
        message.AddUShort(Id);
        message.AddUShort(clientTick);
        message.AddVector3(transform.position);
        message.AddVector3(transform.forward);
        message.AddVector3(playerMovement.rb.velocity);
        message.AddVector3(playerMovement.rb.angularVelocity);
        message.AddQuaternion(transform.rotation);
        NetworkManager.Singleton.Server.SendToAll(message);
    }

    [MessageHandler((ushort)ClientToServerId.PlayerName)]
    private static void PlayerName(ushort fromClientId, Message message)
    {
        Spawn(fromClientId, message.GetString());
    }

    [MessageHandler((ushort)ClientToServerId.PlayerInput)]
    private static void PlayerInput(ushort fromClientId, Message message)
    {
        Player player = List[fromClientId];


        byte inputsQuantity = message.GetByte();
        ClientInput[] inputs = new ClientInput[inputsQuantity];

        // Now we loops to get all the inputs sent by the client and store them in an array 
        for (int i = 0; i < inputsQuantity; i++)
        {
            inputs[i] = new ClientInput
            {
                Inputs = message.GetBools(6),
                currentTick = message.GetUShort(),
                rotation = message.GetQuaternion()
            };
        }

        player.HandleClientInput(inputs, player.Id);
    }
    #endregion
}

