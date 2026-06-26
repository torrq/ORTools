namespace ORTools.Worker.Model
{
    public class MemoryAddresses
    {
        // ── Singleton ──
        public static MemoryAddresses Current => AppConfig.IsHighRate ? HighRate : MidRate;

        // ── Pointers ──
        public int HPBaseAddress { get; init; }
        public int NameAddress { get; init; }
        public int MapAddress { get; init; }
        public int JobAddress { get; init; }
        public int OnlineAddress { get; init; }
        public int CharacterSlotAddress { get; init; }

        public int WeightAddress { get; init; }
        public int MaxWeightAddress { get; init; }
        public int TextInputActiveAddress { get; init; }

        // ── Offsets ──
        public int StatusBufferOffset { get; init; }
        public int MaxSpOffset { get; init; }
        public int ExpOffset { get; init; }
        public int ExpToLevelOffset { get; init; }
        public int LevelOffset { get; init; }
        public int JobLevelOffset { get; init; }

        // ── Predefined Sets ──
        public static readonly MemoryAddresses HighRate = new MemoryAddresses
        {
            HPBaseAddress = 0x010DCE10,
            NameAddress = 0x010DF5D8,
            MapAddress = 0x010D856C,
            JobAddress = 0x010D93D8,
            OnlineAddress = 0x010A2FB0,
            // This reads the 1-byte Character Slot Index (0-indexed). It is located at offset 0x1E from NameAddress.
            CharacterSlotAddress = 0x010DF5F6,
            WeightAddress = 0x010D94B0,
            MaxWeightAddress = 0x010D94AC,
            TextInputActiveAddress = 0x00F33B48,
            StatusBufferOffset = 0x470,
            MaxSpOffset = 4 * 3,
            ExpOffset = 4 * 2,
            ExpToLevelOffset = 4 * 4,
            LevelOffset = 4 * 10,
            JobLevelOffset = 4 * 12
        };

        public static readonly MemoryAddresses MidRate = new MemoryAddresses
        {
            HPBaseAddress = 0x00E8F434,
            NameAddress = 0x00E91C00,
            MapAddress = 0x00E8ABD4,
            JobAddress = 0x00E8BA54,
            OnlineAddress = 0x00E884B1,
            // This reads the 1-byte Character Slot Index (0-indexed). It is located at offset 0x1E from NameAddress.
            CharacterSlotAddress = 0x00E91C1E,
            WeightAddress = 0x00E8BB28,
            MaxWeightAddress = 0x00E8BB24,
            TextInputActiveAddress = 0x00CE6B40,
            StatusBufferOffset = 0x474,
            MaxSpOffset = 4 * 3,
            ExpOffset = 4,
            ExpToLevelOffset = 4 * 3,
            LevelOffset = 4 * 9,
            JobLevelOffset = 4 * 11
        };

        // ── Calculators ──
        public int GetStatusBufferAddress(int hpBaseAddress) => hpBaseAddress + StatusBufferOffset;
        public int GetMaxSpAddress(int hpBaseAddress) => hpBaseAddress + MaxSpOffset;
        public int GetExpAddress(int jobAddress) => jobAddress + ExpOffset;
        public int GetExpToLevelAddress(int jobAddress) => jobAddress + ExpToLevelOffset;
        public int GetLevelAddress(int jobAddress) => jobAddress + LevelOffset;
        public int GetJobLevelAddress(int jobAddress) => jobAddress + JobLevelOffset;
        public int GetBuffStatusAddress(int statusBufferAddress, int effectStatusIndex) => statusBufferAddress + (effectStatusIndex * 4);
    }
}
