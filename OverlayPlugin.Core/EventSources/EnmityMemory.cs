using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace RainbowMage.OverlayPlugin.EventSources
{
    public enum ObjectType : byte
    {
        Unknown = 0x00,
        PC = 0x01,
        Monster = 0x02,
        NPC = 0x03,
        Aetheryte = 0x05,
        Gathering = 0x06,
        Minion = 0x09
    }

    [Serializable]
    public class Combatant
    {
        public uint ID;
        public uint OwnerID;
        public ObjectType Type;
        public uint TargetID;

        public byte Job;
        public string Name;

        public int CurrentHP;
        public int MaxHP;

        public Single PosX;
        public Single PosY;
        public Single PosZ;
        public Single Rotation;

        public string Distance;
        public byte EffectiveDistance;

        public List<EffectEntry> Effects;

        public string DistanceString(Combatant target)
        {
            var distanceX = (float)Math.Abs(PosX - target.PosX);
            var distanceY = (float)Math.Abs(PosY - target.PosY);
            var distanceZ = (float)Math.Abs(PosZ - target.PosZ);
            var distance = (float)Math.Sqrt((distanceX * distanceX) + (distanceY * distanceY) + (distanceZ * distanceZ));
            return distance.ToString("0.00");
        }
    }

    [Serializable]
    public class EnmityEntry
    {
        public uint ID;
        public uint OwnerID;
        public string Name;
        public uint Enmity;
        public bool isMe;
        public int HateRate;
        public byte Job;
    }

    [Serializable]
    public class AggroEntry
    {
        public uint ID;
        public string Name;
        public int HateRate;
        public int Order;
        public bool isCurrentTarget;

        public int CurrentHP;
        public int MaxHP;

        // Target of Enemy
        public EnmityEntry Target;

        // Effects
        public List<EffectEntry> Effects;
    }

    [Serializable]
    public class EffectEntry
    {
        public ushort BuffID;
        public ushort Stack;
        public float Timer;
        public uint ActorID;
        public bool isOwner;
    }

    public class EnmityMemory
    {
        private FFXIVMemory memory;
        private ILogger logger;

        private IntPtr charmapAddress = IntPtr.Zero;
        private IntPtr targetAddress = IntPtr.Zero;
        private IntPtr enmityAddress = IntPtr.Zero;
        private IntPtr aggroAddress = IntPtr.Zero;

        private const string charmapSignature = "48c1ea0381faa7010000????8bc2488d0d";
        private const string targetSignature = "e8f2652f0084c00f8591010000488d0d";
        private const string enmitySignature = "83f9ff7412448b048e8bd3488d0d";

        // Offsets from the signature to find the correct address.
        private const int charmapSignatureOffset = 0;
        private const int targetSignatureOffset = 0;
        private const int enmitySignatureOffset = -4648;

        // Offset from the enmityAddress to find various enmity data structures.
        private const int aggroEnmityOffset = 0x908;

        // Offsets from the targetAddress to find the correct target type.
        private const int targetTargetOffset = 176;
        private const int focusTargetOffset = 184;
        private const int hoverTargetOffset = 248;

        // Constants.
        private const uint emptyID = 0xE0000000;
        private const int numMemoryCombatants = 421;

        public EnmityMemory(ILogger logger)
        {
            this.memory = new FFXIVMemory(logger);
            this.memory.OnProcessChange += ResetPointers;
            this.logger = logger;
            GetPointerAddress();
        }

        private void ResetPointers(object sender, EventArgs _)
        {
            charmapAddress = IntPtr.Zero;
            targetAddress = IntPtr.Zero;
            enmityAddress = IntPtr.Zero;
            aggroAddress = IntPtr.Zero;
        }

        private bool HasValidPointers()
        {
            if (charmapAddress == IntPtr.Zero)
                return false;
            if (targetAddress == IntPtr.Zero)
                return false;
            if (enmityAddress == IntPtr.Zero)
                return false;
            if (aggroAddress == IntPtr.Zero)
                return false;
            return true;
        }

        public bool IsValid()
        {
            if (!memory.IsValid())
                return false;

            if (!HasValidPointers())
                GetPointerAddress();

            if (!HasValidPointers())
                return false;

            return true;
        }

        private bool GetPointerAddress()
        {
            if (!memory.IsValid())
                return false;

            bool success = true;
            bool bRIP = true;

            List<string> fail = new List<string>();

            /// CHARMAP
            List<IntPtr> list = memory.SigScan(charmapSignature, 0, bRIP);
            if (list != null && list.Count > 0)
            {
                charmapAddress = list[0] + charmapSignatureOffset;
            }
            else
            {
                charmapAddress = IntPtr.Zero;
                fail.Add(nameof(charmapAddress));
                success = false;
            }

            // ENMITY
            list = memory.SigScan(enmitySignature, 0, bRIP);
            if (list != null && list.Count > 0)
            {
                enmityAddress = list[0] + enmitySignatureOffset;
                aggroAddress = IntPtr.Add(enmityAddress, aggroEnmityOffset);
            }
            else
            {
                enmityAddress = IntPtr.Zero;
                aggroAddress = IntPtr.Zero;
                fail.Add(nameof(enmityAddress));
                fail.Add(nameof(aggroAddress));
                success = false;
            }

            /// TARGET
            list = memory.SigScan(targetSignature, 0, bRIP);
            if (list != null && list.Count > 0)
            {
                targetAddress = list[0] + targetSignatureOffset;
            }
            else
            {
                targetAddress = IntPtr.Zero;
                fail.Add(nameof(targetAddress));
                success = false;
            }

            logger.Log(LogLevel.Debug, "charmapAddress: 0x{0:X}", charmapAddress.ToInt64());
            logger.Log(LogLevel.Debug, "enmityAddress: 0x{0:X}", enmityAddress.ToInt64());
            logger.Log(LogLevel.Debug, "targetAddress: 0x{0:X}", targetAddress.ToInt64());
            Combatant c = GetSelfCombatant();
            if (c != null)
            {
                logger.Log(LogLevel.Debug, "MyCharacter: '{0}' (0x{1:X})", c.Name, c.ID);
            }

            if (!success)
            {
                logger.Log(LogLevel.Error, "Failed to memory scan: {0}.", String.Join(",", fail));
            }

            return success;
        }

        private Combatant GetTargetRelativeCombatant(int offset)
        {
            IntPtr address = memory.ReadIntPtr(IntPtr.Add(targetAddress, offset));
            if (address == IntPtr.Zero)
                return null;
            byte[] source = memory.GetByteArray(address, CombatantMemory.Size);
            return GetCombatantFromByteArray(source, 0, false);
        }

        public Combatant GetTargetCombatant()
        {
            return GetTargetRelativeCombatant(targetTargetOffset);
        }

        public Combatant GetSelfCombatant()
        {
            IntPtr address = memory.ReadIntPtr(charmapAddress);
            if (address == IntPtr.Zero)
                return null;
            byte[] source = memory.GetByteArray(address, CombatantMemory.Size);
            return GetCombatantFromByteArray(source, 0, true, true);
        }

        public Combatant GetFocusCombatant()
        {
            return GetTargetRelativeCombatant(focusTargetOffset);
        }

        public Combatant GetHoverCombatant()
        {
            return GetTargetRelativeCombatant(hoverTargetOffset);
        }

        public unsafe List<Combatant> GetCombatantList()
        {
            var result = new List<Combatant>();
            var seen = new HashSet<uint>();
            var mychar = GetSelfCombatant();

            int sz = 8;
            byte[] source = memory.GetByteArray(charmapAddress, sz * numMemoryCombatants);
            if (source == null || source.Length == 0)
                return result;

            for (int i = 0; i < numMemoryCombatants; i++)
            {
                IntPtr p;
                fixed (byte* bp = source) p = new IntPtr(*(Int64*)&bp[i * sz]);

                if (p == IntPtr.Zero)
                    continue;

                byte[] c = memory.GetByteArray(p, CombatantMemory.Size);
                Combatant combatant = GetMobFromByteArray(c, mychar == null ? 0 : mychar.ID);
                if (combatant == null)
                    continue;
                if (seen.Contains(combatant.ID))
                    continue;

                // TODO: should this just be a dictionary? there are a lot of id lookups.
                result.Add(combatant);
                seen.Add(combatant.ID);
            }

            return result;
        }

        // Returns a combatant if the combatant is a mob or a PC.
        public unsafe Combatant GetMobFromByteArray(byte[] source, uint mycharID)
        {
            fixed (byte* p = source)
            {
                CombatantMemory mem = *(CombatantMemory*)&p[0];
                ObjectType type = (ObjectType)mem.Type;
                if (type != ObjectType.PC && type != ObjectType.Monster)
                    return null;
                if (mem.ID == 0 || mem.ID == emptyID)
                    return null;
            }
            return GetCombatantFromByteArray(source, mycharID, false);
        }

        [StructLayout(LayoutKind.Explicit)]
        public unsafe struct CombatantMemory
        {
            public static int Size => Marshal.SizeOf(typeof(CombatantMemory));

            // Unknown size, but this is the bytes up to the next field.
            public const int nameBytes = 68;

            // (effect container size: 12) * (Max. effects: 60)
            public const int effectBytes = 720;

            [FieldOffset(0x30)]
            public fixed byte Name[nameBytes];

            [FieldOffset(0x74)]
            public uint ID;

            [FieldOffset(0x84)]
            public uint OwnerID;

            [FieldOffset(0x8C)]
            public byte Type;

            [FieldOffset(0x92)]
            public byte EffectiveDistance;

            [FieldOffset(0xA0)]
            public Single PosX;

            [FieldOffset(0xA4)]
            public Single PosY;

            [FieldOffset(0xA8)]
            public Single PosZ;

            [FieldOffset(0xB0)]
            public Single Rotation;

            [FieldOffset(0x17F0)]
            public uint TargetID;

            [FieldOffset(0x1898)]
            public int CurrentHP;

            [FieldOffset(0x189C)]
            public int MaxHP;

            [FieldOffset(0x18D6)]
            public byte Job;

            [FieldOffset(0x1958)]
            public fixed byte Effects[effectBytes];
        }

        [StructLayout(LayoutKind.Explicit, Size = 12)]
        public struct EffectMemory
        {
            public static int Size => Marshal.SizeOf(typeof(EffectMemory));

            [FieldOffset(0)]
            public ushort BuffID;

            [FieldOffset(2)]
            public ushort Stack;

            [FieldOffset(4)]
            public float Timer;

            [FieldOffset(8)]
            public uint ActorID;
        }

        // Will return any kind of combatant, even if not a mob.
        // This function always returns a combatant object, even if empty.
        public unsafe Combatant GetCombatantFromByteArray(byte[] source, uint mycharID, bool isPlayer, bool exceptEffects = false)
        {
            fixed (byte* p = source)
            {
                CombatantMemory mem = *(CombatantMemory*)&p[0];

                if (isPlayer)
                {
                    mycharID = mem.ID;
                }

                Combatant combatant = new Combatant()
                {
                    Name = FFXIVMemory.GetStringFromBytes(mem.Name, CombatantMemory.nameBytes),
                    Job = mem.Job,
                    ID = mem.ID,
                    OwnerID = mem.OwnerID == emptyID ? 0 : mem.OwnerID,
                    Type = (ObjectType)mem.Type,
                    EffectiveDistance = mem.EffectiveDistance,
                    PosX = mem.PosX,
                    PosY = mem.PosY,
                    PosZ = mem.PosZ,
                    Rotation = mem.Rotation,
                    TargetID = mem.TargetID,
                    CurrentHP = mem.CurrentHP,
                    MaxHP = mem.MaxHP,
                    Effects = exceptEffects ? new List<EffectEntry>() : GetEffectEntries(mem.Effects, (ObjectType)mem.Type, mycharID),
                };
                if (combatant.Type != ObjectType.PC && combatant.Type != ObjectType.Monster)
                {
                    // Other types have garbage memory for hp.
                    combatant.CurrentHP = 0;
                    combatant.MaxHP = 0;
                }
                return combatant;
            }
        }

        [StructLayout(LayoutKind.Explicit, Size=72)]
        struct EnmityListEntry
        {
            public static int Size => Marshal.SizeOf(typeof(EnmityListEntry));

            [FieldOffset(0x38)]
            public uint ID;

            [FieldOffset(0x3C)]
            public uint Enmity;
        }

        // A byte[] -> EnmityListEntry[] converter.
        // Owns the memory and returns out EnmityListEntry objects from it.
        // Both the enmity list and the aggro list use this same structure.
        private class EnmityList
        {
            public int numEntries = 0;
            private byte[] buffer;

            public const short maxEntries = 31;
            public const int numEntryOffset = 0x8F8;
            // The number of entries is a short at the end of the array of entries.  Hence, +2.
            public const int totalBytesSize = numEntryOffset + 2;

            public unsafe EnmityListEntry GetEntry(int i)
            {
                fixed (byte* p = buffer)
                {
                    return *(EnmityListEntry*)&p[i * EnmityListEntry.Size];
                }
            }

            public unsafe EnmityList(byte[] buffer)
            {
                Debug.Assert(maxEntries * EnmityListEntry.Size <= totalBytesSize);
                Debug.Assert(buffer.Length >= totalBytesSize);

                this.buffer = buffer;
                fixed (byte* p = buffer) numEntries = Math.Min((short)p[numEntryOffset], maxEntries);
            }
        }

        private EnmityList ReadEnmityList(IntPtr address)
        {
            return new EnmityList(memory.GetByteArray(address, EnmityList.totalBytesSize));
        }

        // Converts an EnmityList into a List<EnmityEntry>.
        public List<EnmityEntry> GetEnmityEntryList(List<Combatant> combatantList)
        {
            uint topEnmity = 0;
            Combatant mychar = GetSelfCombatant();
            var result = new List<EnmityEntry>();

            EnmityList list = ReadEnmityList(enmityAddress);
            for (int i = 0; i < list.numEntries; i++)
            {
                EnmityListEntry e = list.GetEntry(i);
                topEnmity = Math.Max(topEnmity, e.Enmity);

                Combatant c = null;
                if (e.ID > 0)
                {
                    c = combatantList.Find(x => x.ID == e.ID);
                }

                var entry = new EnmityEntry()
                {
                    ID = e.ID,
                    Enmity = e.Enmity,
                    isMe = e.ID == mychar.ID,
                    Name = c == null ? "Unknown" : c.Name,
                    OwnerID = c == null ? 0 : c.OwnerID,
                    HateRate = (int)(((double)e.Enmity / (double)topEnmity) * 100),
                    Job = c == null ? (byte)0 : c.Job,
                };

                result.Add(entry);
            }
            return result;
        }

        // Converts an EnmityList into a List<AggroEntry>.
        public unsafe List<AggroEntry> GetAggroList(List<Combatant> combatantList)
        {
            Combatant mychar = GetSelfCombatant();

            uint currentTargetID = 0;
            var targetCombatant = GetTargetCombatant();
            if (targetCombatant != null)
            {
                currentTargetID = targetCombatant.ID;
            }

            var result = new List<AggroEntry>();

            EnmityList list = ReadEnmityList(aggroAddress);
            for (int i = 0; i < list.numEntries; i++)
            {
                EnmityListEntry e = list.GetEntry(i);
                if (e.ID <= 0)
                    continue;
                Combatant c = combatantList.Find(x => x.ID == e.ID);
                if (c == null)
                    continue;

                var entry = new AggroEntry()
                {
                    ID = e.ID,
                    // Rather than storing enmity, this is hate rate for the aggro list.
                    // This is likely because we're reading the memory for the aggro sidebar.
                    HateRate = (int)e.Enmity,
                    isCurrentTarget = (e.ID == currentTargetID),
                    Name = c.Name,
                    MaxHP = c.MaxHP,
                    CurrentHP = c.CurrentHP,
                    Effects = c.Effects,
                };

                // TODO: it seems like when your chocobo has aggro, this entry
                // is you, and not your chocobo.  It's not clear if there's
                // anything that can be done about it.
                if (c.TargetID > 0)
                {
                    Combatant t = combatantList.Find(x => x.ID == c.TargetID);
                    if (t != null)
                    {
                        entry.Target = new EnmityEntry()
                        {
                            ID = t.ID,
                            Name = t.Name,
                            OwnerID = t.OwnerID,
                            isMe = mychar.ID == t.ID ? true : false,
                            Enmity = 0,
                            HateRate = 0,
                            Job = t.Job,
                        };
                    }
                }
                result.Add(entry);
            }
            return result;
        }

        public unsafe List<EffectEntry> GetEffectEntries(byte* source, ObjectType type, uint mycharID)
        {
            var result = new List<EffectEntry>();
            int maxEffects = (type == ObjectType.PC) ? 30 : 60;
            var size = EffectMemory.Size * maxEffects;

            var bytes = new byte[size];
            Marshal.Copy((IntPtr)source, bytes, 0, size);

            for (int i = 0; i < maxEffects; i++)
            {
                var effect = GetEffectEntryFromBytes(bytes, i);

                if (effect.BuffID > 0 &&
                    effect.Stack >= 0 &&
                    effect.Timer >= 0.0f &&
                    effect.ActorID > 0)
                {
                    effect.isOwner = effect.ActorID == mycharID;

                    result.Add(effect);
                }
            }

            return result;
        }

        public unsafe EffectEntry GetEffectEntryFromBytes(byte[] source, int num = 0)
        {
            fixed (byte* p = source)
            {
                EffectMemory mem = *(EffectMemory*)&p[num * EffectMemory.Size];

                EffectEntry effectEntry = new EffectEntry()
                {
                    BuffID = mem.BuffID,
                    Stack = mem.Stack,
                    Timer = mem.Timer,
                    ActorID = mem.ActorID,
                    isOwner = false,
                };

                return effectEntry;
            }
        }
    }
}
