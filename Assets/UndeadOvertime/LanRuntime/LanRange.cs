using System;
using System.Collections.Generic;
using UndeadOvertime.Core;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UndeadOvertime.LanPrototype
{
    /// <summary>
    /// Opt-in direct-IP transport/authority test. Players use host-resolved planar walking.
    /// No client-controlled positions or hit reports. One opt-in host-owned placeholder infected; no production art.
    /// </summary>
    public sealed class LanRange : MonoBehaviour
    {
        private const string CommandMessage = "uo.range.command.v4", SnapshotMessage = "uo.range.snapshot.v4";
        [SerializeField] private Camera worldCamera;
        private readonly LanSession session = new LanSession(LanRangeProtocol.Version);
        private readonly Dictionary<ulong, MemberState> members = new Dictionary<ulong, MemberState>();
        private readonly List<Material> materials = new List<Material>();
        private readonly GameObject[] avatars = new GameObject[LanSession.MaximumPlayers];
        private readonly GameObject[] targets = new GameObject[3];
        private readonly int[] health = { 100, 100, 100 };
        private NetworkManager manager;
        private UnityTransport transport;
        private HostFireGate fireGate;
        private HostMoveGate moveGate;
        private HostInfectedEncounter encounter;
        private GameObject infectedObject;
        private Collider infectedCollider;
        private Transform generated;
        private RangeSnapshot snapshot;
        private string address = "127.0.0.1", portText = "7777";
        private string status = "Offline. Loopback is not proof of LAN connectivity.";
        private bool wasBackground, captured, waitForRelease, stopping, handlersRegistered;
        private float yaw, pitch;
        private double nextSnapshotAt, connectDeadline, nextLocalShotAt, nextLocalMoveAt;
        private ulong sequence;
        private int localSlot = -1;
        public bool IsBuilt { get; private set; }
        public bool PresentationEnabled { get; private set; }
        public Camera WorldCamera => worldCamera;
        public bool IsHosting => manager != null && manager.IsHost && !stopping;
        public bool IsConnected => manager != null && manager.IsConnectedClient && !stopping;
        public bool IsBusy => manager != null && (manager.IsListening || manager.ShutdownInProgress || stopping || connectDeadline > 0);
        public int MemberCount => snapshot?.MemberCount ?? 0;
        public SessionPhase Phase => snapshot?.Phase ?? SessionPhase.Stopped;
        public string Status => status;
        public RangeSnapshot CurrentSnapshot => snapshot;
        public bool TryGetLocalMember(out RangeMember member)
        {
            if (snapshot != null && manager != null)
                for (int i = 0; i < snapshot.MemberCount; i++)
                    if (snapshot.Member(i).PeerId == manager.LocalClientId) { member = snapshot.Member(i); return true; }
            member = default; return false;
        }

        private sealed class MemberState
        {
            public PlayerTicket Ticket;
            public byte Slot;
            public bool Connected;
            public double Deadline, NextCommandAt, NextMoveAt, NextReviveAt;
            public Vector3 Position;
            public ulong LastSequence;
        }
        private void Start() { if (!IsBuilt) Build(worldCamera); LanLoopbackSmoke.TryStart(this); }
        public void Build(Camera camera) => Build(camera, true);
        // The explicit false option exercises the same collider-only path in editor regression tests.
        // A null graphics device always disables presentation, even if the caller requests it.
        public void Build(Camera camera, bool enablePresentation)
        {
            if (IsBuilt) return;
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            PresentationEnabled = enablePresentation && SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;
            if (!PresentationEnabled) camera.enabled = false;
            worldCamera = camera; camera.nearClipPlane = 0.05f; camera.farClipPlane = 100f; camera.fieldOfView = 75f;
            generated = new GameObject("LAN range generated geometry").transform; generated.SetParent(transform, false);
            Material floor = Material("Range floor", new Color(.18f, .23f, .25f));
            Material wall = Material("Range wall", new Color(.38f, .42f, .44f));
            Material target = Material("Shared targets", new Color(.95f, .35f, .1f));
            Material body = Material("Remote stations", new Color(.15f, .6f, .8f));
            Material infected = Material("Placeholder infected", new Color(.35f, .55f, .15f));
            Primitive(PrimitiveType.Cube, "Ground", new Vector3(0, -.25f, 0), new Vector3(32, .5f, 32), floor);
            Primitive(PrimitiveType.Cube, "North wall", new Vector3(0, 2, 16), new Vector3(32, 4, .5f), wall);
            Primitive(PrimitiveType.Cube, "South wall", new Vector3(0, 2, -16), new Vector3(32, 4, .5f), wall);
            Primitive(PrimitiveType.Cube, "West wall", new Vector3(-16, 2, 0), new Vector3(.5f, 4, 32), wall);
            Primitive(PrimitiveType.Cube, "East wall", new Vector3(16, 2, 0), new Vector3(.5f, 4, 32), wall);
            for (int i = 0; i < 3; i++) targets[i] = Primitive(PrimitiveType.Sphere, "Shared target " + (i + 1),
                new Vector3((i - 1) * 5, 1.6f, 10), Vector3.one * 1.5f, target);
            for (int i = 0; i < avatars.Length; i++)
            {
                avatars[i] = Primitive(PrimitiveType.Capsule, "Station " + (i + 1), Station(i) - Vector3.up * .7f,
                    new Vector3(.65f, .9f, .65f), body);
                avatars[i].GetComponent<Collider>().enabled = false; avatars[i].SetActive(false);
            }
            infectedObject = Primitive(PrimitiveType.Capsule, "Host-owned infected", ToUnity(HostInfectedEncounter.Spawn) - Vector3.up * .7f,
                new Vector3(.7f, .9f, .7f), infected);
            infectedCollider = infectedObject.GetComponent<Collider>(); infectedObject.SetActive(false);
            camera.transform.SetPositionAndRotation(new Vector3(0, 4, -14), Quaternion.Euler(10, 0, 0));
            IsBuilt = true;
        }
        public static Vector3 Station(int slot) => ToUnity(HostMoveGate.Spawn(slot));
        private static Vector3 ToUnity(AimVector value) => new Vector3((float)value.X, (float)value.Y, (float)value.Z);
        private static AimVector ToCore(Vector3 value) => new AimVector(value.x, value.y, value.z);
        private Material Material(string name, Color color)
        {
            if (!PresentationEnabled) return null;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("LAN range rendering needs an included URP Lit or Standard shader.");
            var material = new Material(shader) { name = name, color = color }; materials.Add(material); return material;
        }
        private GameObject Primitive(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject obj;
            if (PresentationEnabled)
            {
                obj = GameObject.CreatePrimitive(type);
                obj.GetComponent<Renderer>().sharedMaterial = material;
            }
            else
            {
                // Keep authoritative raycasts identical without meshes, shaders or material construction.
                obj = new GameObject(name);
                switch (type)
                {
                    case PrimitiveType.Cube: obj.AddComponent<BoxCollider>().size = Vector3.one; break;
                    case PrimitiveType.Sphere: obj.AddComponent<SphereCollider>().radius = .5f; break;
                    case PrimitiveType.Capsule:
                        var capsule = obj.AddComponent<CapsuleCollider>(); capsule.radius = .5f; capsule.height = 2; capsule.direction = 1; break;
                    default: throw new ArgumentOutOfRangeException(nameof(type), "Unsupported range collider.");
                }
            }
            obj.name = name;
            obj.transform.SetParent(generated, false); obj.transform.position = position; obj.transform.localScale = scale;
            return obj;
        }

        private bool Prepare(string ip, ushort port)
        {
            if (!IsBuilt || IsBusy) { status = "Build the scene and wait for the previous session to stop."; return false; }
            if (!LanEndpoint.TryParse(ip, port.ToString(System.Globalization.CultureInfo.InvariantCulture), out _))
            { status = "Use an explicit private/loopback IPv4 address and port 1-65535."; return false; }
            if (manager == null)
            {
                if (NetworkManager.Singleton != null) { status = "Another NetworkManager exists; open only LanRange."; return false; }
                wasBackground = Application.runInBackground;
                GameObject root = new GameObject("Undead Overtime LAN manager");
                root.SetActive(false);
                transport = root.AddComponent<UnityTransport>(); manager = root.AddComponent<NetworkManager>();
                manager.NetworkConfig = new NetworkConfig
                {
                    NetworkTransport = transport, ConnectionApproval = true, EnableSceneManagement = false,
                    ForceSamePrefabs = false, ProtocolVersion = LanRangeProtocol.Version,
                    ConnectionData = LanRangeProtocol.Handshake, ClientConnectionBufferTimeout = 8,
                    PlayerPrefab = null, TickRate = 30
                };
                transport.ConnectTimeoutMS = 1000; transport.MaxConnectAttempts = 5; transport.DisconnectTimeoutMS = 5000;
                manager.ConnectionApprovalCallback = Approve;
                manager.OnClientConnectedCallback += Connected; manager.OnClientDisconnectCallback += Disconnected;
                manager.OnClientStopped += ClientStopped; manager.OnTransportFailure += TransportFailed;
                root.SetActive(true);
            }
            stopping = false; snapshot = null; localSlot = -1; sequence = 0; connectDeadline = 0;
            members.Clear(); fireGate = null; moveGate = null; encounter = null; nextSnapshotAt = 0; handlersRegistered = false;
            if (session.Phase != SessionPhase.Stopped) session.TryStop(session.Host);
            for (int i = 0; i < 3; i++) health[i] = 100;
            ApplyVisuals(); ReleaseControls(); address = ip; portText = port.ToString(); return true;
        }
        public bool StartHosting(string ip, ushort port)
        {
            if (!Prepare(ip, port)) return false;
            PlayerTicket host = session.StartHost(NetworkManager.ServerClientId);
            members.Add(host.PeerId, new MemberState { Ticket = host, Slot = 0, Position = Station(0), Connected = true });
            transport.SetConnectionData(true, ip, port, ip);
            if (!manager.StartHost()) { StopSession("Host start failed; check address/port and log."); return false; }
            RegisterHandlers(); status = "Hosting " + ip + ":" + port + ". Join peers before starting the round.";
            PublishSnapshot(); return true;
        }
        public bool Join(string ip, ushort port)
        {
            if (!Prepare(ip, port)) return false;
            transport.SetConnectionData(true, ip, port);
            connectDeadline = Time.realtimeSinceStartupAsDouble + 10;
            status = "Connecting to " + ip + ":" + port + "...";
            if (!manager.StartClient()) { StopSession("Client start failed; check log."); return false; }
            RegisterHandlers(); return true;
        }
        private void RegisterHandlers()
        {
            manager.CustomMessagingManager.RegisterNamedMessageHandler(CommandMessage, ReceiveCommand);
            manager.CustomMessagingManager.RegisterNamedMessageHandler(SnapshotMessage, ReceiveSnapshot);
            handlersRegistered = true;
        }
        private void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false; response.Pending = false; response.Approved = false;
            if (request.ClientNetworkId == NetworkManager.ServerClientId) { response.Approved = true; return; }
            if (stopping || !LanRangeProtocol.AcceptHandshake(request.Payload)) { response.Reason = "Wrong range protocol."; return; }
            JoinResult result = session.TryJoin(request.ClientNetworkId, LanRangeProtocol.Version, out PlayerTicket ticket);
            if (result != JoinResult.Accepted) { response.Reason = result == JoinResult.Full ? "Range full (10 includes host)." : "Join only while host is in lobby."; return; }
            byte slot = 0;
            for (; slot < LanSession.MaximumPlayers; slot++)
            {
                bool used = false; foreach (MemberState member in members.Values) if (member.Slot == slot) used = true;
                if (!used) break;
            }
            members.Add(ticket.PeerId, new MemberState { Ticket = ticket, Slot = slot, Position = Station(slot), Deadline = Time.realtimeSinceStartupAsDouble + 10 });
            response.Approved = true;
        }
        private void Connected(ulong peer)
        {
            if (stopping) return;
            if (manager.IsServer)
            {
                if (members.TryGetValue(peer, out MemberState member)) member.Connected = true;
                nextSnapshotAt = 0;
            }
            if (peer == manager.LocalClientId) { connectDeadline = 0; status = "Connected. Waiting for host snapshot/round."; }
        }
        private void Disconnected(ulong peer)
        {
            if (manager.IsServer && members.TryGetValue(peer, out MemberState member))
            { session.Disconnect(member.Ticket); members.Remove(peer); nextSnapshotAt = 0; }
            if (!manager.IsServer && (peer == manager.LocalClientId || !manager.IsConnectedClient))
            {
                string reason = manager.DisconnectReason;
                StopSession(string.IsNullOrEmpty(reason) ? "Disconnected from host." : "Disconnected: " + reason);
            }
        }
        private void ClientStopped(bool wasHost) { FinishStop(); }
        private void TransportFailed() { StopSession("Transport failed. Check address/port; firewall changes require approval."); }
        public void StopSession(string reason = "Disconnected.")
        {
            status = reason; ReleaseControls(); stopping = manager != null && (manager.IsListening || manager.ShutdownInProgress);
            connectDeadline = 0;
            if (session.Phase != SessionPhase.Stopped) session.TryStop(session.Host);
            members.Clear(); fireGate = null; moveGate = null; encounter = null; snapshot = null; localSlot = -1; ApplyVisuals();
            if (manager != null)
            {
                if (handlersRegistered && manager.CustomMessagingManager != null)
                {
                    manager.CustomMessagingManager.UnregisterNamedMessageHandler(CommandMessage);
                    manager.CustomMessagingManager.UnregisterNamedMessageHandler(SnapshotMessage);
                }
                handlersRegistered = false;
                if (manager.IsListening && !manager.ShutdownInProgress) manager.Shutdown();
            }
            if (!stopping) FinishStop();
        }
        private void FinishStop()
        {
            connectDeadline = 0; stopping = false; ReleaseControls();
            if (session.Phase != SessionPhase.Stopped) session.TryStop(session.Host);
            members.Clear(); fireGate = null; moveGate = null; encounter = null; snapshot = null; localSlot = -1; ApplyVisuals();
        }
        public bool BeginRound()
        {
            if (!IsHosting) return false;
            foreach (MemberState member in members.Values) if (!member.Connected) return false;
            if (!session.TryBeginRound(session.Host)) return false;
            for (int i = 0; i < health.Length; i++) health[i] = 100;
            double now = Time.realtimeSinceStartupAsDouble;
            fireGate = new HostFireGate(session, now); moveGate = new HostMoveGate(session, now); encounter = new HostInfectedEncounter(session, now);
            foreach (MemberState member in members.Values)
            { member.LastSequence = 0; member.NextCommandAt = member.NextMoveAt = member.NextReviveAt = 0; member.Position = Station(member.Slot); }
            status = "Round started. Capture controls: WASD move, mouse aim/fire, R reload.";
            PublishSnapshot(); return true;
        }
        public bool StartEncounter()
        {
            if (!IsHosting || encounter == null) return false;
            foreach (MemberState member in members.Values)
                if (member.Connected) encounter.SetPlayerPositionFromHost(member.Ticket, ToCore(member.Position));
            if (!encounter.StartFromHost(Time.realtimeSinceStartupAsDouble)) return false;
            status = "Infected encounter active. Stay together, move and shoot. Hold E near a downed teammate for three seconds to revive.";
            PublishSnapshot(); return true;
        }
        public bool ReturnToLobby()
        {
            if (!IsHosting || !session.TryReturnToLobby(session.Host)) return false;
            fireGate = null; moveGate = null; encounter = null; ReleaseControls(); status = "Lobby. Peers may join; start a new round to reset targets.";
            PublishSnapshot(); return true;
        }
        private void Update()
        {
            if (!IsBuilt) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (connectDeadline > 0 && now >= connectDeadline) StopSession("Connection timed out. Check host address/port and network.");
            if (stopping && manager != null && !manager.IsListening && !manager.ShutdownInProgress) FinishStop();
            if (IsHosting)
            {
                var expired = new List<ulong>();
                foreach (var pair in members) if (!pair.Value.Connected && now >= pair.Value.Deadline) expired.Add(pair.Key);
                foreach (ulong peer in expired)
                { session.Disconnect(members[peer].Ticket); members.Remove(peer); manager.DisconnectClient(peer, "Connection setup expired."); }
                fireGate?.AdvanceReloadsFromHost(now);
                if (now >= nextSnapshotAt) { PublishSnapshot(); nextSnapshotAt = now + .1; }
            }
            if (!captured || !IsConnected || Phase != SessionPhase.Playing) return;
            Keyboard keyboard = Keyboard.current; Mouse mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { ReleaseControls(); return; }
            if (now >= nextLocalMoveAt)
            {
                nextLocalMoveAt = now + .05;
                Vector3 input = Vector3.zero;
                if (keyboard != null)
                {
                    input.x = (keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0);
                    input.z = (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0);
                }
                input = Quaternion.Euler(0, yaw, 0) * Vector3.ClampMagnitude(input, 1);
                SendAction(RangeAction.Move, input);
                SendAction(RangeAction.Revive, keyboard != null && keyboard.eKey.isPressed ? Vector3.forward : Vector3.zero);
            }
            if (mouse == null) return;
            if (!mouse.leftButton.isPressed) waitForRelease = false;
            Vector2 delta = mouse.delta.ReadValue(); yaw = Mathf.Repeat(yaw + delta.x * .12f + 180, 360) - 180;
            pitch = Mathf.Clamp(pitch - delta.y * .12f, -75, 75);
            worldCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) SendAction(RangeAction.Reload, worldCamera.transform.forward);
            if (!waitForRelease && mouse.leftButton.isPressed && now >= nextLocalShotAt)
            { nextLocalShotAt = now + HostFireGate.FireInterval; SendAction(RangeAction.Fire, worldCamera.transform.forward); }
        }
        private void FixedUpdate()
        {
            if (!IsBuilt || !IsHosting || moveGate == null || !moveGate.TryStepFromHost(Time.realtimeSinceStartupAsDouble, out MoveStep[] steps)) return;
            Physics.SyncTransforms();
            foreach (MoveStep step in steps)
                if (members.TryGetValue(step.Player.PeerId, out MemberState member) && member.Connected && member.Ticket.Equals(step.Player))
                    member.Position = ResolvePlanarMovement(member.Position, ToUnity(step.Displacement));
            StepEncounter(Time.realtimeSinceStartupAsDouble);
        }
        private void StepEncounter(double now)
        {
            if (encounter == null) return;
            foreach (MemberState member in members.Values)
                if (member.Connected) encounter.SetPlayerPositionFromHost(member.Ticket, ToCore(member.Position));
            if (encounter.ProposeStepFromHost(now, out AimVector displacement, out PlayerTicket target))
            {
                bool wasEnabled = infectedCollider.enabled;
                infectedCollider.enabled = false; // Exclude only the infected's own collider during its trusted sweep/LOS query.
                try
                {
                    Vector3 position = ResolvePlanarMovement(ToUnity(encounter.Snapshot.Position), ToUnity(displacement));
                    encounter.SetEnemyPositionFromHost(ToCore(position));
                    infectedObject.transform.position = position - Vector3.up * .7f;
                    if (members.TryGetValue(target.PeerId, out MemberState victim) && victim.Ticket.Equals(target))
                    {
                        bool clear = !Physics.Linecast(position, victim.Position, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                        encounter.MeleeFromHost(target, now, clear);
                    }
                }
                finally { infectedCollider.enabled = wasEnabled; }
                Physics.SyncTransforms();
            }
            foreach (MemberState member in members.Values)
                if (member.Connected && encounter.ProposeReviveFromHost(member.Ticket, now, out PlayerTicket downed) &&
                    members.TryGetValue(downed.PeerId, out MemberState victim) && victim.Connected && victim.Ticket.Equals(downed))
                {
                    bool clear = !Physics.Linecast(member.Position, victim.Position, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                    encounter.AdvanceReviveFromHost(member.Ticket, downed, now, clear);
                }
            foreach (MemberState member in members.Values)
            {
                bool alive = encounter.PlayerHealth(member.Ticket) > 0;
                fireGate?.SetAliveFromHost(member.Ticket, alive); moveGate?.SetCanMoveFromHost(member.Ticket, alive);
            }
        }
        // Flat arena only. Player bodies do not block teammates; environment/targets do block walking.
        public static Vector3 ResolvePlanarMovement(Vector3 origin, Vector3 displacement)
        {
            if (!HostMoveGate.ValidPosition(ToCore(origin)) || !ToCore(displacement).IsFinite || displacement.y != 0 ||
                displacement.sqrMagnitude > (float)(HostMoveGate.Speed * HostMoveGate.MaximumStep * HostMoveGate.Speed * HostMoveGate.MaximumStep) + .00001f)
                return origin;
            const float radius = .35f, skin = .02f;
            Vector3 position = origin, remaining = displacement;
            for (int pass = 0; pass < 2 && remaining.sqrMagnitude > .00000001f; pass++)
            {
                float distance = remaining.magnitude; Vector3 direction = remaining / distance;
                if (!Physics.CapsuleCast(position + Vector3.down * 1.2f, position + Vector3.down * .2f,
                    radius, direction, out RaycastHit hit, distance + skin, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                { position += remaining; break; }
                float travel = Mathf.Clamp(hit.distance - skin, 0, distance);
                position += direction * travel;
                remaining = Vector3.ProjectOnPlane(remaining - direction * travel, hit.normal); remaining.y = 0;
            }
            position.x = Mathf.Clamp(position.x, -(float)HostMoveGate.ArenaLimit, (float)HostMoveGate.ArenaLimit);
            position.z = Mathf.Clamp(position.z, -(float)HostMoveGate.ArenaLimit, (float)HostMoveGate.ArenaLimit);
            position.y = (float)HostMoveGate.EyeHeight; return position;
        }
        public void CaptureControls()
        {
            if (!PresentationEnabled || !IsConnected || Phase != SessionPhase.Playing || localSlot < 0 ||
                !TryGetLocalMember(out RangeMember player) || player.Health == 0) return;
            captured = true; waitForRelease = true; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }
        public void ReleaseControls()
        {
            if (captured && IsConnected && Phase == SessionPhase.Playing)
            { SendAction(RangeAction.Move, Vector3.zero); SendAction(RangeAction.Revive, Vector3.zero); }
            captured = false; waitForRelease = true;
            if (PresentationEnabled) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }
        private void OnApplicationFocus(bool focused) { if (!focused) ReleaseControls(); }
        private void OnDisable() { if (IsBuilt) StopSession("Range disabled."); }
        public bool SendAction(RangeAction action, Vector3 aim)
        {
            if (!IsConnected || snapshot == null || Phase != SessionPhase.Playing || sequence == ulong.MaxValue) return false;
            var direction = new AimVector(aim.x, aim.y, aim.z);
            if (action == RangeAction.Move ? !HostMoveGate.ValidIntent(direction) :
                action == RangeAction.Revive ? !HostInfectedEncounter.ValidReviveIntent(direction) : !direction.IsUnit) return false;
            var command = new RangeCommand(action, snapshot.SessionId, snapshot.RoundId, ++sequence, direction);
            if (IsHosting) HandleCommand(manager.LocalClientId, command);
            else Send(CommandMessage, NetworkManager.ServerClientId, LanRangeProtocol.Encode(command), NetworkDelivery.ReliableSequenced);
            return true;
        }
        private void ReceiveCommand(ulong sender, FastBufferReader reader)
        {
            if (!IsHosting || !members.TryGetValue(sender, out MemberState member) || !member.Connected ||
                !TryReadPayload(reader, LanRangeProtocol.CommandBytes, LanRangeProtocol.CommandBytes, out byte[] bytes)) return;
            if (LanRangeProtocol.TryCommand(bytes, out RangeCommand command)) HandleCommand(sender, command);
        }
        private void HandleCommand(ulong sender, RangeCommand command)
        {
            if (!IsHosting || fireGate == null || !members.TryGetValue(sender, out MemberState member) || !member.Connected ||
                command.SessionId != session.SessionId || command.RoundId != session.RoundId ||
                command.Sequence == 0 || command.Sequence <= member.LastSequence ||
                (command.Action == RangeAction.Move ? !HostMoveGate.ValidIntent(command.Aim) :
                    command.Action == RangeAction.Revive ? !HostInfectedEncounter.ValidReviveIntent(command.Aim) : !command.Aim.IsUnit)) return;
            member.LastSequence = command.Sequence;
            double now = Time.realtimeSinceStartupAsDouble;
            if (command.Action == RangeAction.Revive)
            {
                if (encounter != null && (now >= member.NextReviveAt || command.Aim.Z == 0))
                {
                    member.NextReviveAt = now + .02;
                    encounter.HoldReviveFromHost(member.Ticket, command.Aim.Z == 1, now);
                }
                return;
            }
            if (command.Action == RangeAction.Move)
            {
                if (command.Aim.LengthSquared > 0) encounter?.CancelReviveFromHost(member.Ticket);
                // Movement and fire have independent pacing so a walking packet cannot eat a shot.
                // A fresh explicit stop must not be dropped just because it follows a walk packet.
                if (moveGate != null && (now >= member.NextMoveAt || command.Aim.LengthSquared == 0))
                {
                    member.NextMoveAt = now + .02;
                    moveGate.TrySetIntent(member.Ticket, command.SessionId, command.RoundId, command.Sequence, command.Aim, now);
                }
                return;
            }
            encounter?.CancelReviveFromHost(member.Ticket);
            if (now < member.NextCommandAt) return;
            member.NextCommandAt = now + .02;
            Vector3 origin = member.Position;
            fireGate.SetPoseFromHost(member.Ticket, new AimVector(origin.x, origin.y, origin.z), command.Aim);
            if (command.Action == RangeAction.Reload) fireGate.TryBeginReloadFromHost(member.Ticket, now);
            else if (command.Action == RangeAction.Fire)
            {
                var request = new FireRequest(command.SessionId, command.RoundId, command.Sequence,
                    new AimVector(origin.x, origin.y, origin.z), command.Aim);
                if (fireGate.TryAuthorize(member.Ticket, request, now, out AuthorizedShot shot) == FireResult.Accepted)
                {
                    Physics.SyncTransforms();
                    if (Physics.Raycast(origin, new Vector3((float)shot.Direction.X, (float)shot.Direction.Y, (float)shot.Direction.Z),
                        out RaycastHit hit, (float)shot.Range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider == infectedCollider && encounter != null)
                        {
                            encounter.DamageEnemyFromHost(member.Ticket, shot.Damage);
                            if (encounter.Phase == EncounterPhase.Cleared) infectedObject.SetActive(false);
                        }
                        else for (int i = 0; i < targets.Length; i++) if (hit.collider.gameObject == targets[i])
                            { health[i] = Math.Max(0, health[i] - shot.Damage); ApplyTarget(i); }
                    }
                }
            }
            nextSnapshotAt = 0;
        }
        private void PublishSnapshot()
        {
            if (!IsHosting || session.Phase == SessionPhase.Stopped) return;
            var rows = new List<RangeMember>();
            foreach (PlayerTicket ticket in session.SnapshotMembers())
                if (members.TryGetValue(ticket.PeerId, out MemberState m) && m.Connected)
                {
                    byte targetSlot = 255, progress = 0;
                    if (encounter != null && encounter.ReviveStatusFromHost(ticket, Time.realtimeSinceStartupAsDouble, out PlayerTicket target, out progress) &&
                        members.TryGetValue(target.PeerId, out MemberState downed) && downed.Connected && downed.Ticket.Equals(target)) targetSlot = downed.Slot;
                    else progress = 0;
                    rows.Add(new RangeMember(ticket.PeerId, ticket.ConnectionId, m.Slot,
                        (byte)(fireGate?.Ammo(ticket) ?? 0), fireGate?.IsReloading(ticket) ?? false, ToCore(m.Position), (byte)(encounter?.PlayerHealth(ticket) ?? 100), targetSlot, progress));
                }
            if (rows.Count == 0) return;
            var state = new RangeSnapshot(session.SessionId, session.RoundId, session.Phase, rows.ToArray(), health, encounter?.Snapshot ?? InfectedSnapshot.Dormant);
            ApplySnapshot(state); byte[] bytes = LanRangeProtocol.Encode(state);
            foreach (var row in rows) if (row.PeerId != manager.LocalClientId)
                Send(SnapshotMessage, row.PeerId, bytes, NetworkDelivery.ReliableSequenced);
        }
        private void ReceiveSnapshot(ulong sender, FastBufferReader reader)
        {
            if (manager == null || manager.IsServer || stopping || sender != NetworkManager.ServerClientId ||
                !TryReadPayload(reader, LanRangeProtocol.SnapshotHeaderBytes, LanRangeProtocol.MaximumBytes, out byte[] bytes)) return;
            if (LanRangeProtocol.TrySnapshot(bytes, out RangeSnapshot state)) ApplySnapshot(state);
        }
        // NGO named messages can leave an already-consumed hash prefix in this reader.
        // Bounds apply to unread payload, not the whole underlying message buffer.
        public static bool TryReadPayload(FastBufferReader reader, int minimum, int maximum, out byte[] bytes)
        {
            bytes = null;
            int remaining = reader.Length - reader.Position;
            if (minimum < 0 || maximum > LanRangeProtocol.MaximumBytes || remaining < minimum || remaining > maximum) return false;
            bytes = new byte[remaining]; reader.ReadBytesSafe(ref bytes, remaining); return true;
        }
        private void Send(string name, ulong peer, byte[] bytes, NetworkDelivery delivery)
        {
            using (var writer = new FastBufferWriter(bytes.Length, Allocator.Temp))
            { writer.WriteBytesSafe(bytes); manager.CustomMessagingManager.SendNamedMessage(name, peer, writer, delivery); }
        }
        private void ApplySnapshot(RangeSnapshot state)
        {
            bool changed = snapshot == null || state.SessionId != snapshot.SessionId || state.RoundId != snapshot.RoundId;
            bool encounterChanged = snapshot == null || state.Encounter.Phase != snapshot.Encounter.Phase;
            if (!IsHosting && (changed || snapshot.Phase != state.Phase))
                status = state.Phase == SessionPhase.Playing ? "Host round active. Capture controls to aim/fire." : "Connected to host lobby.";
            if (encounterChanged && state.Encounter.Phase == EncounterPhase.Cleared) status = "Infected cleared. Return to lobby for a fresh round.";
            if (encounterChanged && state.Encounter.Phase == EncounterPhase.Failed) status = "All connected survivors are down. Host can return to lobby and restart.";
            snapshot = state; localSlot = -1;
            for (int i = 0; i < state.MemberCount; i++) if (state.Member(i).PeerId == manager.LocalClientId) localSlot = state.Member(i).Slot;
            if (changed) { sequence = 0; yaw = 0; pitch = 0; nextLocalShotAt = nextLocalMoveAt = 0; ReleaseControls(); }
            if (state.Phase != SessionPhase.Playing) ReleaseControls();
            for (int i = 0; i < 3; i++) health[i] = state.TargetHealth(i);
            ApplyVisuals();
            if (TryGetLocalMember(out RangeMember local))
            {
                worldCamera.transform.position = ToUnity(local.Position);
                if (local.Health == 0) ReleaseControls();
            }
        }
        private void ApplyVisuals()
        {
            if (!IsBuilt) return;
            for (int i = 0; i < avatars.Length; i++) if (avatars[i] != null) avatars[i].SetActive(false);
            if (snapshot != null) for (int i = 0; i < snapshot.MemberCount; i++)
            {
                RangeMember row = snapshot.Member(i);
                avatars[row.Slot].transform.position = ToUnity(row.Position) - Vector3.up * (row.Health > 0 ? .7f : 1.35f);
                avatars[row.Slot].transform.localScale = row.Health > 0 ? new Vector3(.65f, .9f, .65f) : new Vector3(.65f, .25f, .65f);
                avatars[row.Slot].SetActive(row.Slot != localSlot);
            }
            for (int i = 0; i < 3; i++) ApplyTarget(i);
            if (infectedObject != null)
            {
                InfectedSnapshot enemy = snapshot?.Encounter ?? InfectedSnapshot.Dormant;
                infectedObject.transform.position = ToUnity(enemy.Position) - Vector3.up * .7f;
                infectedObject.SetActive(enemy.Phase != EncounterPhase.Dormant && enemy.Health > 0);
            }
        }
        private void ApplyTarget(int index) { if (targets[index] != null) targets[index].SetActive(health[index] > 0); }
        private void OnGUI()
        {
            if (!IsBuilt || !PresentationEnabled) return;
            GUILayout.BeginArea(new Rect(14, 14, 570, 360), GUI.skin.box);
            GUILayout.Label("UNDEAD OVERTIME | LAN transport firing range");
            GUILayout.Label("Host-owned walking, targets and an optional infected encounter. Prototype art.");
            GUILayout.Label(status);
            if (!IsBusy)
            {
                GUILayout.Label("Host binds this exact local IP. Client enters host IP. Default is loopback.");
                address = GUILayout.TextField(address, 15); portText = GUILayout.TextField(portText, 5);
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Host") && LanEndpoint.TryParse(address, portText, out ushort hp)) StartHosting(address, hp);
                if (GUILayout.Button("Join") && LanEndpoint.TryParse(address, portText, out ushort jp)) Join(address, jp);
                GUILayout.EndHorizontal();
            }
            else
            {
                GUILayout.Label("Players: " + MemberCount + "/10 | " + Phase + " | Round " + (snapshot?.RoundId ?? 0));
                GUILayout.Label("Target HP: " + health[0] + " / " + health[1] + " / " + health[2]);
                if (snapshot != null) GUILayout.Label("Encounter: " + snapshot.Encounter.Phase + " | Infected HP " + snapshot.Encounter.Health);
                if (snapshot != null) for (int i = 0; i < snapshot.MemberCount; i++)
                    if (snapshot.Member(i).PeerId == manager.LocalClientId)
                        GUILayout.Label("Your slot " + (snapshot.Member(i).Slot + 1) + " | Health " + snapshot.Member(i).Health + (snapshot.Member(i).Health == 0 ? " (DOWNED)" : "") + " | Ammo " + snapshot.Member(i).Ammo +
                            (snapshot.Member(i).Reloading ? " | Reloading..." : " | R reload"));
                if (TryGetLocalMember(out RangeMember local) && local.ReviveTargetSlot != 255)
                    GUILayout.Label("Reviving slot " + (local.ReviveTargetSlot + 1) + ": " + local.ReviveProgress + "%");
                GUILayout.BeginHorizontal();
                if (IsHosting && Phase == SessionPhase.Lobby && GUILayout.Button("Start round")) BeginRound();
                if (IsHosting && Phase == SessionPhase.Playing && snapshot.Encounter.Phase == EncounterPhase.Dormant && GUILayout.Button("Start infected")) StartEncounter();
                if (IsHosting && Phase == SessionPhase.Playing && GUILayout.Button("Return to lobby")) ReturnToLobby();
                if (Phase == SessionPhase.Playing && !captured && GUILayout.Button("Capture mouse to aim/fire")) CaptureControls();
                if (GUILayout.Button("Disconnect")) StopSession();
                GUILayout.EndHorizontal();
                GUILayout.Label("WASD walk, R reload, hold E nearby to revive, Esc releases controls. Flat ground only; no jump/prediction yet.");
            }
            GUILayout.EndArea();
            if (captured) GUI.Label(new Rect(Screen.width / 2f - 5, Screen.height / 2f - 10, 20, 20), "+");
        }
        private void OnDestroy()
        {
            StopSession("Range closed.");
            if (manager != null)
            {
                manager.ConnectionApprovalCallback = null;
                manager.OnClientConnectedCallback -= Connected; manager.OnClientDisconnectCallback -= Disconnected;
                manager.OnClientStopped -= ClientStopped; manager.OnTransportFailure -= TransportFailed;
                Destroy(manager.gameObject); Application.runInBackground = wasBackground;
            }
            foreach (Material material in materials) if (material != null) Destroy(material);
        }
    }
}
