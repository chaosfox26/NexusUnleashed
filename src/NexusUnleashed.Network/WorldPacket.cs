using System;
using System.Collections.Generic;
using NexusUnleashed.Cryptography;

namespace NexusUnleashed.Network;

public static class WorldPacket
{
    public const ushort ServerContainer = 0x03DC;
    public const ushort ClientContainer = 0x0244;

    public const ulong WorldChannelSeed = PacketCrypt.AuthChannelKey;

    public static byte[] EncodeServer(ushort innerOpcode, byte[] innerBody, PacketCrypt crypt)
    {
        byte[] payload = BuildContainerPayload(innerOpcode, innerBody, crypt);
        return GamePacketFrame.Encode(ServerContainer, payload);
    }

    public static byte[] EncodeClient(ushort innerOpcode, byte[] innerBody, PacketCrypt crypt)
    {
        byte[] payload = BuildContainerPayload(innerOpcode, innerBody, crypt);
        return GamePacketFrame.Encode(ClientContainer, payload);
    }

    public static (ushort Opcode, byte[] Body) DecodeContainer(byte[] containerPayload, PacketCrypt crypt)
    {
        if (containerPayload.Length < 4)
            throw new ArgumentException("container payload shorter than its length field");

        uint innerLen = ReadU32LE(containerPayload, 0);        if (innerLen < 4 || innerLen > containerPayload.Length)
            throw new ArgumentException($"container innerLen {innerLen} out of range ({containerPayload.Length})");

        int cipherLen = (int)innerLen - 4;
        var cipher = new byte[cipherLen];
        Array.Copy(containerPayload, 4, cipher, 0, cipherLen);

        byte[] inner = crypt.Decrypt(cipher, cipherLen);
        if (inner.Length < 2)
            throw new ArgumentException("decrypted inner shorter than an opcode");

        ushort opcode = (ushort)(inner[0] | (inner[1] << 8));
        var body = new byte[inner.Length - 2];
        Array.Copy(inner, 2, body, 0, body.Length);
        return (opcode, body);
    }

    private static byte[] BuildContainerPayload(ushort innerOpcode, byte[] innerBody, PacketCrypt crypt)
    {
        var inner = new byte[2 + innerBody.Length];
        inner[0] = (byte)innerOpcode;
        inner[1] = (byte)(innerOpcode >> 8);
        Array.Copy(innerBody, 0, inner, 2, innerBody.Length);

        byte[] cipher = crypt.EncryptForClient(inner, inner.Length);

        uint innerLen = (uint)(4 + cipher.Length);
        var payload = new byte[4 + cipher.Length];
        WriteU32LE(payload, 0, innerLen);
        Array.Copy(cipher, 0, payload, 4, cipher.Length);
        return payload;
    }

    private static uint ReadU32LE(byte[] b, int off)
        => (uint)(b[off] | (b[off + 1] << 8) | (b[off + 2] << 16) | (b[off + 3] << 24));

    private static void WriteU32LE(byte[] b, int off, uint v)
    {
        b[off] = (byte)v;
        b[off + 1] = (byte)(v >> 8);
        b[off + 2] = (byte)(v >> 16);
        b[off + 3] = (byte)(v >> 24);
    }
}
