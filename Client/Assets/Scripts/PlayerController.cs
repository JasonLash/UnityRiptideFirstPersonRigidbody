using System;
using UnityEngine;
using Riptide;

public class ClientInput
{
    public bool[] Inputs = new bool[6];
    public ushort currentTick = 0;
    public Quaternion rotation;
}

public class SimulationState
{
    public Vector3 position;
    public Quaternion rotation;
    public ushort currentTick = 0;
}

public class ServerSimulationState
{
    public Vector3 position;
    public Quaternion rotation;
    public Vector3 velocity;
    public Vector3 angularVelocity;
    public ushort currentTick = 0;
}

public class PlayerController : MonoBehaviour
{
    public ushort cTick;
    public const int CacheSize = 1024;

    private ClientInput[] inputCache;
    private SimulationState[] clientStateCache;

    public Player playerScript;

    private int lastCorrectedFrame;

    private Vector3 clientPosError;
    private Quaternion clientRotError;

    public PlayerMovement Move;

    private void Awake()
    {
        playerScript = GetComponent<Player>();
    }

    private void Start()
    {
        Physics.simulationMode = SimulationMode.Script;

        inputCache = new ClientInput[CacheSize];
        clientStateCache = new SimulationState[CacheSize];

        clientPosError = Vector3.zero;
        clientRotError = Quaternion.identity;

        cTick = 0;
    }

    private void Update()
    {
        int cacheIndex = cTick % CacheSize;
        inputCache[cacheIndex] = GetInput();
    }

    private void FixedUpdate()
    {
        int cacheIndex = cTick % CacheSize;

        inputCache[cacheIndex] = GetInput();

        clientStateCache[cacheIndex] = CurrentSimulationState();


        Move.PhysicsStep(inputCache[cacheIndex].Inputs, Move.orientation.transform.rotation);
        Physics.Simulate(NetworkManager.Singleton.TickRate);
        SendInput();

        ++cTick;

        if (playerScript.serverSimulationState != null) Reconciliate();
    }

    private void Reconciliate()
    {
        if (playerScript.serverSimulationState.currentTick <= lastCorrectedFrame) return;


        ServerSimulationState serverSimulationState = playerScript.serverSimulationState;

        uint cacheIndex = (uint)serverSimulationState.currentTick % CacheSize;
        SimulationState cachedSimulationState = clientStateCache[cacheIndex];


        Vector3 positionError = serverSimulationState.position - cachedSimulationState.position;
        //float rotationError = 1.0f - Quaternion.Dot(serverSimulationState.rotation, cachedSimulationState.rotation);


        if (positionError.sqrMagnitude > 0.0000001f)
        {
            Debug.Log("Correcting for error at tick " + serverSimulationState.currentTick + " (rewinding " + (cTick - cachedSimulationState.currentTick) + " ticks)");
            // capture the current predicted pos for smoothing
            Vector3 prevPos = Move.rb.position + clientPosError;
            Quaternion prevRot = Move.orientation.rotation * clientRotError;

            // rewind & replay
            Move.rb.position = serverSimulationState.position;
            Move.orientation.rotation = serverSimulationState.rotation;
            Move.rb.velocity = serverSimulationState.velocity;
            Move.rb.angularVelocity = serverSimulationState.angularVelocity;

            uint rewindTickNumber = serverSimulationState.currentTick;
            while (rewindTickNumber < cTick)
            {
                cacheIndex = rewindTickNumber % CacheSize;

                clientStateCache[cacheIndex] = CurrentSimulationState();

                Move.PhysicsStep(inputCache[cacheIndex].Inputs, inputCache[cacheIndex].rotation);
                Physics.Simulate(NetworkManager.Singleton.TickRate);

                ++rewindTickNumber;
            }

            // if more than 2ms apart, just snap
            if ((prevPos - Move.rb.position).sqrMagnitude >= 4.0f)
            {
                clientPosError = Vector3.zero;
                clientRotError = Quaternion.identity;
            }
            else
            {
                clientPosError = prevPos - Move.rb.position;
                clientRotError = Quaternion.Inverse(Move.orientation.rotation) * prevRot;
            }
        }
        lastCorrectedFrame = playerScript.serverSimulationState.currentTick;
    }



    private SimulationState CurrentSimulationState()
    {
        return new SimulationState
        {
            position = Move.rb.position,
            rotation = Move.orientation.transform.rotation,
            currentTick = cTick
        };
    }

    private ClientInput GetInput()
    {
        bool[] tempInputs = new bool[6];
        if (Input.GetKey(KeyCode.W))
            tempInputs[0] = true;

        if (Input.GetKey(KeyCode.S))
            tempInputs[1] = true;

        if (Input.GetKey(KeyCode.A))
            tempInputs[2] = true;

        if (Input.GetKey(KeyCode.D))
            tempInputs[3] = true;

        if (Input.GetKey(KeyCode.Space))
            tempInputs[4] = true;

        if (Input.GetKey(KeyCode.LeftControl))
            tempInputs[5] = true;

        return new ClientInput
        {
            Inputs = tempInputs,
            rotation = Move.orientation.transform.rotation,
            currentTick = cTick
        };
    }

    #region Messages
    private void SendInput()
    {
        Message message = Message.Create(MessageSendMode.Unreliable, ClientToServerId.PlayerInput);

        message.AddByte((byte)(cTick - playerScript.serverSimulationState.currentTick));

        for (int i = playerScript.serverSimulationState.currentTick; i < cTick; i++)
        {
            message.AddBools(inputCache[i % CacheSize].Inputs, false);
            message.AddUShort(inputCache[i % CacheSize].currentTick);
            message.AddQuaternion(inputCache[i % CacheSize].rotation);
        }
        NetworkManager.Singleton.Client.Send(message);
    }
    #endregion
}

