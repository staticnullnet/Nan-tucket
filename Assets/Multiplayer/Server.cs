using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Net;
using System.Net.Sockets;

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

            // Registering the custom INetSerializable struct in the packet processor
            packetProcessor.RegisterNestedType<PlayerState>();


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
            server.PollEvents();
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
