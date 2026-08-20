#pragma once
#include <cstdint>
#include <cstring>
#include <string>
#include <vector>
#include <stdexcept>

namespace nexus::net {

class PacketWriter {
public:
    void WriteBit(bool value) {
        if (bit_pos_ == 0) data_.push_back(0);
        if (value) data_.back() |= static_cast<uint8_t>(1u << bit_pos_);
        if (++bit_pos_ == 8) bit_pos_ = 0;
    }

    void WriteBits(uint64_t value, int count) {
        if (count < 0 || count > 64) throw std::out_of_range("WriteBits count");
        for (int i = 0; i < count; ++i)
            WriteBit((value & (1ull << i)) != 0);
    }

    void WriteByte(uint8_t v)   { WriteBits(v, 8); }
    void WriteUInt16(uint16_t v){ WriteBits(v, 16); }
    void WriteInt16(int16_t v)  { WriteBits(static_cast<uint16_t>(v), 16); }
    void WriteUInt32(uint32_t v){ WriteBits(v, 32); }
    void WriteInt32(int32_t v)  { WriteBits(static_cast<uint32_t>(v), 32); }
    void WriteUInt64(uint64_t v){ WriteBits(v, 64); }
    void WriteInt64(int64_t v)  { WriteBits(static_cast<uint64_t>(v), 64); }
    void WriteBool(bool v)      { WriteBit(v); }

    void WriteSingle(float v) {
        uint32_t bits; std::memcpy(&bits, &v, 4); WriteUInt32(bits);
    }
    void WriteDouble(double v) {
        uint64_t bits; std::memcpy(&bits, &v, 8); WriteUInt64(bits);
    }

    void WriteWideString(const std::u16string& s) {
        for (char16_t c : s) WriteUInt16(static_cast<uint16_t>(c));
    }

    void AlignToByte() { if (bit_pos_ != 0) bit_pos_ = 0; }

    void WriteBytes(const uint8_t* p, size_t n) {
        AlignToByte();
        data_.insert(data_.end(), p, p + n);
    }

    std::vector<uint8_t> ToArray() { AlignToByte(); return data_; }

private:
    std::vector<uint8_t> data_;
    int bit_pos_ = 0;
};

class PacketReader {
public:
    PacketReader(const uint8_t* data, size_t size) : data_(data), size_(size) {}
    explicit PacketReader(const std::vector<uint8_t>& v) : data_(v.data()), size_(v.size()) {}

    bool ReadBit() {
        size_t byte = bit_pos_ >> 3;
        if (byte >= size_) throw std::out_of_range("PacketReader underflow");
        bool v = (data_[byte] >> (bit_pos_ & 7)) & 1;
        ++bit_pos_;
        return v;
    }

    uint64_t ReadBits(int count) {
        uint64_t v = 0;
        for (int i = 0; i < count; ++i)
            if (ReadBit()) v |= (1ull << i);
        return v;
    }

    uint8_t  ReadByte()   { return static_cast<uint8_t>(ReadBits(8)); }
    uint16_t ReadUInt16() { return static_cast<uint16_t>(ReadBits(16)); }
    uint32_t ReadUInt32() { return static_cast<uint32_t>(ReadBits(32)); }
    uint64_t ReadUInt64() { return ReadBits(64); }
    float    ReadSingle() { uint32_t b = ReadUInt32(); float f; std::memcpy(&f,&b,4); return f; }

    void AlignToByte() { bit_pos_ = (bit_pos_ + 7) & ~static_cast<size_t>(7); }

    size_t BytesRemaining() const { return size_ - ((bit_pos_ + 7) >> 3); }

    std::vector<uint8_t> ReadBytes(size_t n) {
        AlignToByte();
        size_t byte = bit_pos_ >> 3;
        if (byte + n > size_) throw std::out_of_range("ReadBytes underflow");
        std::vector<uint8_t> out(data_ + byte, data_ + byte + n);
        bit_pos_ += n * 8;
        return out;
    }

private:
    const uint8_t* data_;
    size_t size_;
    size_t bit_pos_ = 0;
};

} // namespace nexus::net
