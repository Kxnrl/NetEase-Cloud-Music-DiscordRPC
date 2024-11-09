using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace NetEaseMusic_DiscordRPC
{
/*
namespace audio {
    struct AudioPlayer {
           private: char pad_0[0x50]; public:
           player::AudioPlayInfo* pAudioPlayInfo; // 0x50
           private: char pad_58[0x8]; public:
           int iPlayerStatus; // 0x60
           float flVolume; // 0x64
           float flPlayVolume; // 0x68
           private: char pad_6c[0x3c]; public:
           double Duration; // 0xa8
           private: char pad_b0[0x50]; public:
    }; // Size: 0x100
}
namespace player {
    struct AudioPlayInfo {
        void* vtable; // 0x0
        void* vtable2; // 0x8
        std::string audioInfo; // 0x10
    }; // Size: 0x30
}

namespace std {
    struct string {
       char* ptr; // 0x0
       private: char pad_8[0x8]; public:
       uint64_t size; // 0x10
       uint64_t allocated; // 0x18
   }; // Size: 0x20
}
*/
    internal class Memory
    {
        private int           _pid;
        private IntPtr        _processPtr;
        private ProcessModule _module;
        private List<byte>    _textSectionBytes;
        private IntPtr        _textSectionStart;

        private IntPtr _audioPlayerOffset;
        private IntPtr _currentPlayLengthOffset;

        public Memory(int pid)
        {
            UpdateProcessByPid(pid);
        }

        public void UpdateProcessByPid(int pid)
        {
            if (_pid == pid)
                return;

            using var process = Process.GetProcessById(pid);

            foreach (ProcessModule module in process.Modules)
            {
                if (!"cloudmusic.dll".Equals(module.ModuleName))
                    continue;

                _module     = module;
                _pid        = pid;
                _processPtr = OpenProcess(0x10, IntPtr.Zero, pid);

                ScanForTextModule();
                FindOffsets();
                break;
            }
        }

        // 歌放到哪里
        public double GetPlayedDuration() => ReadDouble(_currentPlayLengthOffset);
        // 0是刚开播放器, 1是在放歌, 2是暂停, 3跟4还不清楚，其中有一个是在缓冲?
        public int GetPlayerStatus() => ReadInt32(_audioPlayerOffset, 0x60);
        public float GetPlayerVolume() => ReadFloat(_audioPlayerOffset, 0x64);
        public float GetCurrentVolume() => ReadFloat(_audioPlayerOffset, 0x68);
        // 歌的总时长
        public double GetSongDuration() => ReadFloat(_audioPlayerOffset, 0xa8);

        public string GetCurrentSongId()
        {
            // 如果刚开exe没放歌时会是空的， 但PlayerStatus也是0
            var audioPlayInfo = ReadInt64(_audioPlayerOffset, 0x50);
            if (audioPlayInfo == 0)
                return string.Empty;

            var strPtr        = audioPlayInfo + 0x10; // +0x10跳过两个vtable

            var strLength = ReadInt64((IntPtr)strPtr, 0x10);

            // small string optimization
            byte[] strBuffer;
            if (strLength < 15)
            {
                strBuffer = ReadBytes((IntPtr)strPtr, (int)strLength);
            }
            else
            {
                var strAddress = ReadInt64((IntPtr)strPtr);
                strBuffer = ReadBytes((IntPtr)strAddress, (int)strLength);
            }

            var str = Encoding.UTF8.GetString(strBuffer);
            return string.IsNullOrEmpty(str) ? string.Empty : str.Substring(0, str.IndexOf('_'));
        }

        private void FindOffsets()
        {
            var audioPlayerAddress =
                ScanText("48 8D 0D ? ? ? ? E8 ? ? ? ? 48 8D 0D ? ? ? ? E8 ? ? ? ? 90 48 8D 0D ? ? ? ? E8 ? ? ? ? 48 8D 05 ? ? ? ? 48 8D A5 ? ? ? ? 5F 5D C3 CC CC CC CC CC 48 89 4C 24 ? 55 57 48 81 EC ? ? ? ? 48 8D 6C 24 ? 48 8D 7C 24") +
                3;
            var displacement = ReadInt32(audioPlayerAddress);
            _audioPlayerOffset = audioPlayerAddress + 4 + displacement;

            Debug.Print($"_audioPlayerOffset: {_audioPlayerOffset.ToInt64() - _module.BaseAddress.ToInt64():X}");

            var currentPlayLengthAddress = ScanText("66 0F 2E 0D ? ? ? ? 7A ? 75 ? 66 0F 2E 15") + 4;
            displacement             = ReadInt32(currentPlayLengthAddress);
            _currentPlayLengthOffset = currentPlayLengthAddress + 4 + displacement;
            Debug.Print($"_currentPlayLengthOffset: {_currentPlayLengthOffset.ToInt64() - _module.BaseAddress.ToInt64():X}");
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(int dwDesiredAccess, IntPtr bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr pHandle, IntPtr Address, byte[] Buffer, int Size, IntPtr NumberofBytesRead);

        private void ScanForTextModule()
        {
            var module = _module;
            if (module == null)
                throw new Exception("mainModule == null");

            var baseAddress = module.BaseAddress;
            var ntNewOffset = ReadInt32(baseAddress, 0x3C);
            var ntHeader    = baseAddress + ntNewOffset;

            // IMAGE_NT_HEADER
            var fileHeader  = ntHeader + 4;
            var numSections = ReadInt16(ntHeader, 6);

            // IMAGE_OPTIONAL_HEADER
            var optionalHeader = fileHeader + 20;

            var sectionHeader = optionalHeader + 240;

            var sectionCursor = sectionHeader;
            for (var i = 0; i < numSections; i++)
            {
                var sectionName = ReadInt64(sectionCursor);

                // .text
                switch (sectionName)
                {
                    case 0x747865742E: // .text
                        var offset = ReadInt32(sectionCursor, 12);
                        _textSectionStart = baseAddress + offset;

                        var size = ReadInt32(sectionCursor, 8);

                        var buffer = ReadBytes(_textSectionStart, size);
                        _textSectionBytes = buffer.ToList();
                        return;
                }

                sectionCursor += 40;
            }
        }

        private byte[] ReadBytes(IntPtr offset, int length)
        {
            var bytes = new byte[length];
            ReadProcessMemory(_processPtr, offset, bytes, length, IntPtr.Zero);
            return bytes;
        }

        public float  ReadFloat(IntPtr  address, int offset = 0) => BitConverter.ToSingle(ReadBytes(IntPtr.Add(address, offset), 4), 0);
        public double ReadDouble(IntPtr address, int offset = 0) => BitConverter.ToDouble(ReadBytes(IntPtr.Add(address, offset), 8), 0);
        public long   ReadInt64(IntPtr  address, int offset = 0) => BitConverter.ToInt64(ReadBytes(IntPtr.Add(address, offset), 8), 0);
        public ulong  ReadUInt64(IntPtr address, int offset = 0) => BitConverter.ToUInt64(ReadBytes(IntPtr.Add(address, offset), 8), 0);
        public short  ReadInt16(IntPtr  address, int offset = 0) => BitConverter.ToInt16(ReadBytes(IntPtr.Add(address, offset), 2), 0);
        public int    ReadInt32(IntPtr  address, int offset = 0) => BitConverter.ToInt32(ReadBytes(IntPtr.Add(address, offset), 4), 0);

        private static ushort[] ParseSignature(string signature)
        {
            var bytesStr = signature.Split(' ');
            var bytes    = new ushort[bytesStr.Length];

            for (var i = 0; i < bytes.Length; i++)
            {
                var str = bytesStr[i];
                if (str.Contains('?'))
                {
                    bytes[i] = 0xFFFF;
                    continue;
                }

                bytes[i] = Convert.ToByte(str, 16);
            }

            return bytes;
        }

        private IntPtr ScanText(string signature)
        {
            var bytes = ParseSignature(signature);

            var firstByte = bytes[0];

            var scanRet = IntPtr.Zero;

            var scanSize = _textSectionBytes.Count - bytes.Length;
            for (var i = 0; i < scanSize; i++)
            {
                if (firstByte != 0xFFFF && (i = _textSectionBytes.IndexOf((byte)firstByte, i)) == -1) break;

                var found = true;

                for (var j = 1; j < bytes.Length; j++)
                {
                    var isWildCard = bytes[j] == 0xFFFF;
                    var isEqual    = bytes[j] == _textSectionBytes[j + i];

                    if (isWildCard || isEqual) continue;
                    found = false;
                    break;
                }

                if (!found)
                    continue;
                scanRet = _textSectionStart + i;
                break;
            }

            return scanRet;
        }
    }
}