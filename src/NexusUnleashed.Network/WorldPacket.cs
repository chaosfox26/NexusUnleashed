// NexusUnleashed - clean-room authored. The world channel's encrypted packed
// container (spec/protocol/containers.md), decoded byte-for-byte from a real
// login capture. Every world game message rides inside a container:
//
//   outer frame : [u32 size][u16 containerOpcode][container payload]
//   container   : [u32 innerLen self-inclusive][encrypted inner]
//   inner (dec) : [u16 opcode][bit-packed body]
//
// containerOpcode = 0x03DC server->client, 0x0244 client->server. The inner
// message is enciphered with the static build-seeded PacketCrypt; the cipher's
// length counter keys on the inner message length. All of this is a protocol
// fact (the client runs the identical cipher and framing); the code is ours.
using System;
using System.Collections.Generic;
using NexusUnleashed.Cryptography;

namespace NexusUnleashed.Network;

/// <summary>
/// Encode/decode the world channel's encrypted packed container. Proven against
/// the real captured ServerHello: a decode of the captured 0x03DC yields inner
/// opcode 0x0003, and an encode of that inner reproduces the captured bytes.
/// </summary>
public static class WorldPacket
{
    /// <summary>Server-&gt;client container opcode (encrypted envelope).</summary>
    public const ushort ServerContainer = 0x03DC;
    /// <summary>Client-&gt;server container opcode (encrypted envelope).</summary>
    public const ushort ClientContainer = 0x0244;

    /// <summary>
    /// The AUTH-phase cipher key (== PacketCrypt.GetKeyFromAuthBuildAndMessage(),
    /// a build constant). The channel opens with this for the hello, then RE-KEYS
    /// to PacketCrypt.GetKeyFromTicket(sessionKey) after login (two-phase keying).
    /// Kept as a const so the acceptor can seed a session before auth completes.
    /// </summary>
    public const ulong WorldChannelSeed = 0xD283F5B34A8DC685ul;

    /// <summary>
    /// Build a complete on-wire server-&gt;client frame carrying one encrypted game
    /// message. Returns the full outer frame bytes ([size][0x03DC][container]).
    /// </summary>
    public static byte[] EncodeServer(ushort innerOpcode, byte[] innerBody, PacketCrypt crypt)
    {
        byte[] payload = BuildContainerPayload(innerOpcode, innerBody, crypt);
        return GamePacketFrame.Encode(ServerContainer, payload);
    }

    /// <summary>
    /// Build a complete on-wire client-&gt;server frame (mirror of the server path;
    /// same container + symmetric cipher). Used by the reference client / tests.
    /// </summary>
    public static byte[] EncodeClient(ushort innerOpcode, byte[] innerBody, PacketCrypt crypt)
    {
        byte[] payload = BuildContainerPayload(innerOpcode, innerBody, crypt);
        return GamePacketFrame.Encode(ClientContainer, payload);
    }

    /// <summary>
    /// Decode a received container's PAYLOAD (the bytes after [size][opcode], i.e.
    /// what GamePacketFrame.Decode hands back) into the inner game message. The
    /// payload begins with the self-inclusive innerLen u32.
    /// </summary>
    public static (ushort Opcode, byte[] Body) DecodeContainer(byte[] containerPayload, PacketCrypt crypt)
    {
        if (containerPayload.Length < 4)
            throw new ArgumentException("container payload shorter than its length field");

        uint innerLen = ReadU32LE(containerPayload, 0);        // self-inclusive
        if (innerLen < 4 || innerLen > containerPayload.Length)
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

    // container payload = [u32 innerLen self-inclusive][encrypted [u16 op][body]]
    private static byte[] BuildContainerPayload(ushort innerOpcode, byte[] innerBody, PacketCrypt crypt)
    {
        var inner = new byte[2 + innerBody.Length];
        inner[0] = (byte)innerOpcode;
        inner[1] = (byte)(innerOpcode >> 8);
        Array.Copy(innerBody, 0, inner, 2, innerBody.Length);

        byte[] cipher = crypt.Encrypt(inner, inner.Length);

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
