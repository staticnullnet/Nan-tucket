using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Net;
using System.Net.Sockets;
using System.Linq;

namespace Multiplayer
{
    public class Server : MonoBehaviour, INetEventListener
    {
        [SerializeField] public Vector2 initialPosition = new Vector2();
        private NetDataWriter writer;
        private NetPacketProcessor packetProcessor;
        private Dictionary<uint, ServerPlayer> players = new Dictionary<uint, ServerPlayer>();

        private NetManager server;
        private readonly int MAX_CONNECTED_PEERS = 1;

        public void OnConnectionRequest(ConnectionRequest request)
        {
            if (server.ConnectedPeersCount < MAX_CONNECTED_PEERS)
            {
                Debug.Log($"Incoming connection from {request.RemoteEndPoint.ToString()}");
                request.Accept();
            }
            else Debug.Log("Connection refused. Too many connections.");
        }

        // Start is called before the first frame update
        void Start()
        {
            writer = new NetDataWriter();
            packetProcessor = new NetPacketProcessor();
            packetProcessor.RegisterNestedType((w, v) => w.Put(v), reader => reader.GetVector2());

            // Will reuse the same packet class instance instead of creating new ones, so make sure to not store references to it or its contents! 
            packetProcessor.SubscribeReusable<JoinPacket, NetPeer>(OnJoinReceived);
            packetProcessor.SubscribeReusable<PlayerSendUpdatePacket, NetPeer>(OnPlayerUpdate);
                       
            // Registering the custom INetSerializable struct in the packet processor
            packetProcessor.RegisterNestedType<PlayerState>();
            packetProcessor.RegisterNestedType<ClientPlayer>();


            server = new NetManager(this)
            {
                AutoRecycle = true,
            };         
        }

        public void StartServer()
        {
            Debug.Log("Starting server");
            server.Start(12345);
        }

        // Update is called once per frame
        void Update()
        {
            if (server != null)
                server.PollEvents();
            else
                Debug.Log("No server started");

            PlayerState[] states = players.Values.Select(p => p.state).ToArray();
            foreach (ServerPlayer player in players.Values)
            {
                SendPacket(new PlayerReceiveUpdatePacket { states = states }, player.peer, DeliveryMethod.Unreliable);
            }
        }

        public void SendPacket<T>(T packet, NetPeer peer, DeliveryMethod deliveryMethod) where T : class, new()
        {
            if (peer != null)
            {
                writer.Reset();
                packetProcessor.Write(writer, packet);
                peer.Send(writer, deliveryMethod);
            }
        }

        /// <summary>
        /// Receiving a join connection packet, initiating the player using PlayerState struct and sending the accept back to requester.
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="peer"></param>
        public void OnJoinReceived(JoinPacket packet, NetPeer peer)
        {
            Debug.Log($"Received join from {packet.username} (pid: {(uint)peer.Id})");

            ServerPlayer newPlayer = (players[(uint)peer.Id] = new ServerPlayer
            {
                peer = peer,
                state = new PlayerState
                {
                    pid = (uint)peer.Id,
                    position = initialPosition,
                },
                username = packet.username,
            });


            // DeliveryMethod ReliableOrdered is reliable, but Unreliable is faster, where a dropped packet or two won't matter too much.
            SendPacket(new JoinAcceptPacket { state = newPlayer.state }, peer, DeliveryMethod.ReliableOrdered);


            foreach (ServerPlayer player in players.Values)
            {
                if (player.state.pid != newPlayer.state.pid)
                {
                    SendPacket(new PlayerJoinedGamePacket
                    {
                        player = new ClientPlayer
                        {
                            username = newPlayer.username,
                            state = newPlayer.state,
                        },
                    }, player.peer, DeliveryMethod.ReliableOrdered);

                    SendPacket(new PlayerJoinedGamePacket
                    {
                        player = new ClientPlayer
                        {
                            username = player.username,
                            state = player.state,
                        },
                    }, newPlayer.peer, DeliveryMethod.ReliableOrdered);
                }
            }
        }
        public void OnPlayerUpdate(PlayerSendUpdatePacket packet, NetPeer peer)
        {
            players[(uint)peer.Id].state.position = packet.position;
        }
        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            Debug.Log($"Player (pid: {(uint)peer.Id}) left the game");
            if (peer.Tag != null)
            {
                ServerPlayer playerLeft;
                if (players.TryGetValue(((uint)peer.Id), out playerLeft))
                {
                    foreach (ServerPlayer player in players.Values)
                    {
                        if (player.state.pid != playerLeft.state.pid)
                        {
                            SendPacket(new PlayerLeftGamePacket { pid = playerLeft.state.pid }, player.peer, DeliveryMethod.ReliableOrdered);
                        }
                    }
                    players.Remove((uint)peer.Id);
                }
            }
        }

        void INetEventListener.OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
            
        }

        void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
        }

        void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            // You can pass anything you want as a second argument to be used by the packet callbacks. In this case we only need the peer.
            packetProcessor.ReadAllPackets(reader, peer);

        }

        void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
         
        }

        void INetEventListener.OnPeerConnected(NetPeer peer)
        {
         
        }

        void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            if (peer.Tag != null)
            {
                players.Remove((uint)peer.Id);
            }
        }
    } 
}
