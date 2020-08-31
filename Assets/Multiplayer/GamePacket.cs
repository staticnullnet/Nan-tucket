using LiteNetLib;
using LiteNetLib.Utils;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Multiplayer
{
    public class JoinPacket
    {
        public string username { get; set; }
    }

    public class JoinAcceptPacket
    {
        public PlayerState state { get; set; }
    }

    public struct PlayerState : INetSerializable
    {
        public uint pid;
        public Vector2 position;

        public void Serialize(NetDataWriter writer)
        {
            writer.Put(pid);
            writer.Put(position);
        }

        public void Deserialize(NetDataReader reader)
        {
            pid = reader.GetUInt();
            position = reader.GetVector2();
        }
    }

    public class ClientPlayer
    {
        public PlayerState state;
        public string username;
    }

    public class ServerPlayer
    {
        public NetPeer peer;
        public PlayerState state;
        public string username;
    }    
}