namespace LosSantosStrike
{
    /// <summary>Jenkins one-at-a-time hash, the hash GTA V uses for weapon and component names.</summary>
    public static class Joaat
    {
        public static uint Hash(string text)
        {
            uint h = 0;
            foreach (char ch in text.ToLowerInvariant())
            {
                h += ch;
                h += h << 10;
                h ^= h >> 6;
            }
            h += h << 3;
            h ^= h >> 11;
            h += h << 15;
            return h;
        }
    }
}
