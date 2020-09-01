using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;
using System.Net;
using System.Net.Sockets;

namespace Multiplayer
{
    public class Client : MonoBehaviour, INetEventListener
    {
        private NetManager client;
        private NetPeer server;
        private NetDataWriter writer;
        private NetPacketProcessor packetProcessor;
        private ClientPlayer player = new ClientPlayer();
        private int _ping;


        public void Connect(string ip, string username)
        {
            player.username = username;
            writer = new NetDataWriter();
            packetProcessor = new NetPacketProcessor();
            packetProcessor.RegisterNestedType((w, v) => w.Put(v), reader => reader.GetVector2());
            
            packetProcessor.RegisterNestedType<PlayerState>();
            packetProcessor.RegisterNestedType<ClientPlayer>();

            packetProcessor.SubscribeReusable<JoinAcceptPacket>(OnJoinAccept);
            packetProcessor.SubscribeReusable<PlayerReceiveUpdatePacket>(OnReceiveUpdate);
            packetProcessor.SubscribeReusable<PlayerJoinedGamePacket>(OnPlayerJoin);
            packetProcessor.SubscribeReusable<PlayerLeftGamePacket>(OnPlayerLeave);

            client = new NetManager(this)
            {
                AutoRecycle = true,
            };

            client.Start();
            Debug.Log("Connecting to server");
            client.Connect(ip, 12345, "");
        }

        public void SendPacket<T>(T packet, DeliveryMethod deliveryMethod) where T : class, new()
        {
            if (server != null)
            {
                writer.Reset();
                packetProcessor.Write(writer, packet);
                server.Send(writer, deliveryMethod);
            }
        }

        public void OnJoinAccept(JoinAcceptPacket packet)
        {
            Debug.Log($"Join accepted by server (pid: {packet.state.pid})");
            player.state = packet.state;
            BasePlayer.instance.transform.position = player.state.position;
        }

        public void OnPeerConnected(NetPeer peer)
        {
            Debug.Log("Connected to server");
            server = peer;
            SendPacket(new JoinPacket { username = player.username }, DeliveryMethod.ReliableOrdered);
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            packetProcessor.ReadAllPackets(reader);
        }


        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            if (client != null)
            {
                client.PollEvents();
                if (BasePlayer.instance != null)
                {
                    SendPacket(new PlayerSendUpdatePacket { position = BasePlayer.instance.transform.position }, DeliveryMethod.Unreliable);
                }
            }
        }

        public void OnReceiveUpdate(PlayerReceiveUpdatePacket packet)
        {
            foreach (PlayerState state in packet.states)
            {
                if (state.pid == player.state.pid)
                {
                    continue;
                }

                //((RemotePlayer)BasePlayer.instance.getcomp.GetNode(state.pid.ToString())).Position = state.position;
            }
        }

        public void OnPlayerJoin(PlayerJoinedGamePacket packet)
        {
            Debug.Log($"Player '{packet.player.username}' (pid: {packet.player.state.pid}) joined the game");
            RemotePlayer remote = new RemotePlayer();
            
            remote.name = packet.player.state.pid.ToString();
            remote.transform.position = packet.player.state.position;


            //GameObject = Instantiate<GameObject>(DeathFX, transform.position, Quaternion.identity);                        
            //BasePlayer.instance.GetParent().AddChild(remote);
        }

        public void OnPlayerLeave(PlayerLeftGamePacket packet)
        {
            Debug.Log($"Player (pid: {packet.pid}) left the game");
            //((RemotePlayer)BasePlayer.instance.GetParent().GetNode(packet.pid.ToString())).QueueFree(); //destroy player object
        }


        void INetEventListener.OnConnectionRequest(ConnectionRequest request)
        {
            
        }

        void INetEventListener.OnNetworkError(IPEndPoint endPoint, SocketError socketError)
        {
            
        }

        void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
            _ping = latency;
        }

        void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
        {
            
        }

        void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
            
        }
        
        void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            
        }
    }
}