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
    /// Opt-in direct-IP transport/authority test. Players occupy fixed firing stations.
    /// No client-controlled positions, client hit reports, production art or zombie networking.
    /// </summary>
    public sealed class LanRange : MonoBehaviour
    {
        private const string CommandMessage = "uo.range.command.v1", SnapshotMessage = "uo.range.snapshot.v1";
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
        private Transform generated;
        private RangeSnapshot snapshot;
        private string address = "127.0.0.1", portText = "7777";
        private string status = "Offline. Loopback is not proof of LAN connectivity.";
        private bool wasBackground, captured, waitForRelease, stopping, handlersRegistered;
        private float yaw, pitch;
        private double nextSnapshotAt, connectDeadline, nextLocalShotAt;
        private ulong sequence;
        private int localSlot = -1;
        public bool IsBuilt { get; private set; }
        public Camera WorldCamera => worldCamera;
        public bool IsHosting => manager != null && manager.IsHost && !stopping;
        public bool IsConnected => manager != null && manager.IsConnectedClient && !stopping;
        public bool IsBusy => manager != null && (manager.IsListening || manager.ShutdownInProgress || stopping || connectDeadline > 0);
        public int MemberCount => snapshot?.MemberCount ?? 0;
        public SessionPhase Phase => snapshot?.Phase ?? SessionPhase.Stopped;
        public string Status => status;
        public RangeSnapshot CurrentSnapshot => snapshot;

        private sealed class MemberState
        {
            public PlayerTicket Ticket;
            public byte Slot;
            public bool Connected;
            public double Deadline, NextCommandAt;
            public ulong LastSequence;
        }
        private void Start() { if (!IsBuilt) Build(worldCamera); LanLoopbackSmoke.TryStart(this); }
        public void Build(Camera camera)
        {
            if (IsBuilt) return;
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            worldCamera = camera; camera.nearClipPlane = 0.05f; camera.farClipPlane = 100f; camera.fieldOfView = 75f;
            generated = new GameObject("LAN range generated geometry").transform; generated.SetParent(transform, false);
            Material floor = Material("Range floor", new Color(.18f, .23f, .25f));
            Material wall = Material("Range wall", new Color(.38f, .42f, .44f));
            Material target = Material("Shared targets", new Color(.95f, .35f, .1f));
            Material body = Material("Remote stations", new Color(.15f, .6f, .8f));
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
            camera.transform.SetPositionAndRotation(new Vector3(0, 4, -14), Quaternion.Euler(10, 0, 0));
            IsBuilt = true;
        }
        public static Vector3 Station(int slot) => new Vector3(-9 + slot * 2, 1.6f, -8);
        private Material Material(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var material = new Material(shader) { name = name, color = color }; materials.Add(material); return material;
        }
        private GameObject Primitive(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
        {
            GameObject obj = GameObject.CreatePrimitive(type); obj.name = name;
            obj.transform.SetParent(generated, false); obj.transform.position = position; obj.transform.localScale = scale;
            obj.GetComponent<Renderer>().sharedMaterial = material; return obj;
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
            members.Clear(); fireGate = null; nextSnapshotAt = 0; handlersRegistered = false;
            if (session.Phase != SessionPhase.Stopped) session.TryStop(session.Host);
            for (int i = 0; i < 3; i++) health[i] = 100;
            ApplyVisuals(); ReleaseControls(); address = ip; portText = port.ToString(); return true;
        }
        public bool StartHosting(string ip, ushort port)
        {
            if (!Prepare(ip, port)) return false;
            PlayerTicket host = session.StartHost(NetworkManager.ServerClientId);
            members.Add(host.PeerId, new MemberState { Ticket = host, Slot = 0, Connected = true });
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
            members.Add(ticket.PeerId, new MemberState { Ticket = ticket, Slot = slot, Deadline = Time.realtimeSinceStartupAsDouble + 10 });
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
            members.Clear(); fireGate = null; snapshot = null; localSlot = -1; ApplyVisuals();
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
            members.Clear(); fireGate = null; snapshot = null; localSlot = -1; ApplyVisuals();
        }
        public bool BeginRound()
        {
            if (!IsHosting) return false;
            foreach (MemberState member in members.Values) if (!member.Connected) return false;
            if (!session.TryBeginRound(session.Host)) return false;
            for (int i = 0; i < health.Length; i++) health[i] = 100;
            fireGate = new HostFireGate(session, Time.realtimeSinceStartupAsDouble);
            foreach (MemberState member in members.Values) { member.LastSequence = 0; member.NextCommandAt = 0; }
            status = "Round started. Capture controls to aim/fire; R reloads.";
            PublishSnapshot(); return true;
        }
        public bool ReturnToLobby()
        {
            if (!IsHosting || !session.TryReturnToLobby(session.Host)) return false;
            fireGate = null; ReleaseControls(); status = "Lobby. Peers may join; start a new round to reset targets.";
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
            if (mouse == null) return;
            if (!mouse.leftButton.isPressed) waitForRelease = false;
            Vector2 delta = mouse.delta.ReadValue(); yaw = Mathf.Repeat(yaw + delta.x * .12f + 180, 360) - 180;
            pitch = Mathf.Clamp(pitch - delta.y * .12f, -75, 75);
            worldCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame) SendAction(RangeAction.Reload, worldCamera.transform.forward);
            if (!waitForRelease && mouse.leftButton.isPressed && now >= nextLocalShotAt)
            { nextLocalShotAt = now + HostFireGate.FireInterval; SendAction(RangeAction.Fire, worldCamera.transform.forward); }
        }
        public void CaptureControls()
        {
            if (!IsConnected || Phase != SessionPhase.Playing || localSlot < 0) return;
            captured = true; waitForRelease = true; Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }
        public void ReleaseControls() { captured = false; waitForRelease = true; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        private void OnApplicationFocus(bool focused) { if (!focused) ReleaseControls(); }
        private void OnDisable() { if (IsBuilt) StopSession("Range disabled."); }
        public bool SendAction(RangeAction action, Vector3 aim)
        {
            if (!IsConnected || snapshot == null || Phase != SessionPhase.Playing || sequence == ulong.MaxValue) return false;
            var direction = new AimVector(aim.x, aim.y, aim.z);
            if (!direction.IsUnit) return false;
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
                command.Sequence == 0 || command.Sequence <= member.LastSequence || !command.Aim.IsUnit) return;
            member.LastSequence = command.Sequence;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now < member.NextCommandAt) return;
            member.NextCommandAt = now + .02;
            Vector3 origin = Station(member.Slot);
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
                        for (int i = 0; i < targets.Length; i++) if (hit.collider.gameObject == targets[i])
                        { health[i] = Math.Max(0, health[i] - shot.Damage); ApplyTarget(i); }
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
                    rows.Add(new RangeMember(ticket.PeerId, ticket.ConnectionId, m.Slot,
                        (byte)(fireGate?.Ammo(ticket) ?? 0), fireGate?.IsReloading(ticket) ?? false));
            if (rows.Count == 0) return;
            var state = new RangeSnapshot(session.SessionId, session.RoundId, session.Phase, rows.ToArray(), health);
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
            if (!IsHosting && (changed || snapshot.Phase != state.Phase))
                status = state.Phase == SessionPhase.Playing ? "Host round active. Capture controls to aim/fire." : "Connected to host lobby.";
            snapshot = state; localSlot = -1;
            for (int i = 0; i < state.MemberCount; i++) if (state.Member(i).PeerId == manager.LocalClientId) localSlot = state.Member(i).Slot;
            if (changed) { sequence = 0; yaw = 0; pitch = 0; nextLocalShotAt = 0; ReleaseControls(); }
            if (state.Phase != SessionPhase.Playing) ReleaseControls();
            for (int i = 0; i < 3; i++) health[i] = state.TargetHealth(i);
            ApplyVisuals();
            if (localSlot >= 0) worldCamera.transform.position = Station(localSlot);
        }
        private void ApplyVisuals()
        {
            if (!IsBuilt) return;
            for (int i = 0; i < avatars.Length; i++) if (avatars[i] != null) avatars[i].SetActive(false);
            if (snapshot != null) for (int i = 0; i < snapshot.MemberCount; i++)
            { RangeMember row = snapshot.Member(i); avatars[row.Slot].SetActive(row.Slot != localSlot); }
            for (int i = 0; i < 3; i++) ApplyTarget(i);
        }
        private void ApplyTarget(int index) { if (targets[index] != null) targets[index].SetActive(health[index] > 0); }
        private void OnGUI()
        {
            if (!IsBuilt) return;
            GUILayout.BeginArea(new Rect(14, 14, 510, 310), GUI.skin.box);
            GUILayout.Label("UNDEAD OVERTIME | LAN transport firing range");
            GUILayout.Label("Fixed stations, shared targets. Not the multiplayer zombie game yet.");
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
                if (snapshot != null) for (int i = 0; i < snapshot.MemberCount; i++)
                    if (snapshot.Member(i).PeerId == manager.LocalClientId)
                        GUILayout.Label("Your station " + (snapshot.Member(i).Slot + 1) + " | Ammo " + snapshot.Member(i).Ammo +
                            (snapshot.Member(i).Reloading ? " | Reloading..." : " | R reload"));
                GUILayout.BeginHorizontal();
                if (IsHosting && Phase == SessionPhase.Lobby && GUILayout.Button("Start round")) BeginRound();
                if (IsHosting && Phase == SessionPhase.Playing && GUILayout.Button("Return to lobby")) ReturnToLobby();
                if (Phase == SessionPhase.Playing && !captured && GUILayout.Button("Capture mouse to aim/fire")) CaptureControls();
                if (GUILayout.Button("Disconnect")) StopSession();
                GUILayout.EndHorizontal();
                GUILayout.Label("Esc releases controls; simulation keeps running for peers. No movement yet.");
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
