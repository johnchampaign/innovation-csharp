namespace Innovation.Core;

/// <summary>
/// Round-trippable snapshot of a <see cref="GameState"/> as a short base64
/// string. Used to emit a per-turn "setup code" into the log so a position
/// can be restored exactly for debugging or replay.
///
/// Layout (version 1), all little-endian:
///   magic "IV1" (3 bytes), numPlayers, phase, activePlayer, actionsRemaining,
///   currentTurn (u16), ageAchMask (u16, bits 1..10), specialAchMask (u8,
///   Monument=0 Empire=1 World=2 Wonder=3 Universe=4), winners (count + idx),
///   decks 1..10 (count + ids), then per player:
///       hand, 5 stacks (count + splay + ids top→bottom), score,
///       ageAch (count + ages), specialMask, scoredThisTurn, tuckedThisTurn.
/// Card IDs and ages fit in one byte (≤105 cards, ages ≤10).
/// </summary>
public static class GameStateCodec
{
    private const byte Version = 1;
    private static readonly string[] SpecialAchOrder =
        { "Monument", "Empire", "World", "Wonder", "Universe" };

    public static string Encode(GameState g)
    {
        var w = new List<byte>(512);
        w.Add((byte)'I'); w.Add((byte)'V'); w.Add(Version);

        w.Add((byte)g.Players.Length);
        w.Add((byte)g.Phase);
        w.Add((byte)g.ActivePlayer);
        w.Add((byte)g.ActionsRemaining);
        WriteU16(w, (ushort)g.CurrentTurn);

        ushort ageMask = 0;
        foreach (var a in g.AvailableAgeAchievements) ageMask |= (ushort)(1 << a);
        WriteU16(w, ageMask);

        byte specialMask = 0;
        for (int i = 0; i < SpecialAchOrder.Length; i++)
            if (g.AvailableSpecialAchievements.Contains(SpecialAchOrder[i]))
                specialMask |= (byte)(1 << i);
        w.Add(specialMask);

        w.Add((byte)g.Winners.Count);
        foreach (var wi in g.Winners) w.Add((byte)wi);

        for (int age = 1; age <= 10; age++)
            WriteIds(w, g.Decks[age]);

        foreach (var p in g.Players)
        {
            WriteIds(w, p.Hand);
            for (int c = 0; c < 5; c++)
            {
                var st = p.Stacks[c];
                w.Add((byte)st.Count);
                w.Add((byte)st.Splay);
                foreach (var id in st.Cards) w.Add((byte)id);
            }
            WriteIds(w, p.ScorePile);

            w.Add((byte)p.AgeAchievements.Count);
            foreach (var a in p.AgeAchievements) w.Add((byte)a);

            byte pSpecial = 0;
            for (int i = 0; i < SpecialAchOrder.Length; i++)
                if (p.SpecialAchievements.Contains(SpecialAchOrder[i]))
                    pSpecial |= (byte)(1 << i);
            w.Add(pSpecial);

            w.Add((byte)p.ScoredThisTurn);
            w.Add((byte)p.TuckedThisTurn);
        }

        return Convert.ToBase64String(w.ToArray());
    }

    public static GameState Decode(string code, IReadOnlyList<Card> cards)
    {
        var bytes = Convert.FromBase64String(code);
        int i = 0;
        if (bytes.Length < 3 || bytes[0] != (byte)'I' || bytes[1] != (byte)'V')
            throw new InvalidDataException("Not an Innovation state code.");
        i = 2;
        byte version = bytes[i++];
        if (version != Version)
            throw new InvalidDataException($"Unsupported state code version {version}.");

        int numPlayers = bytes[i++];
        var g = new GameState(cards, numPlayers);
        g.Phase = (GamePhase)bytes[i++];
        g.ActivePlayer = bytes[i++];
        g.ActionsRemaining = bytes[i++];
        g.CurrentTurn = ReadU16(bytes, ref i);

        ushort ageMask = ReadU16(bytes, ref i);
        g.AvailableAgeAchievements.Clear();
        for (int a = 1; a <= 10; a++)
            if ((ageMask & (1 << a)) != 0) g.AvailableAgeAchievements.Add(a);

        byte specialMask = bytes[i++];
        g.AvailableSpecialAchievements.Clear();
        for (int s = 0; s < SpecialAchOrder.Length; s++)
            if ((specialMask & (1 << s)) != 0)
                g.AvailableSpecialAchievements.Add(SpecialAchOrder[s]);

        int winnerCount = bytes[i++];
        g.Winners.Clear();
        for (int k = 0; k < winnerCount; k++) g.Winners.Add(bytes[i++]);

        for (int age = 1; age <= 10; age++)
        {
            int n = bytes[i++];
            g.Decks[age].Clear();
            for (int k = 0; k < n; k++) g.Decks[age].Add(bytes[i++]);
        }

        foreach (var p in g.Players)
        {
            int hn = bytes[i++];
            p.Hand.Clear();
            for (int k = 0; k < hn; k++) p.Hand.Add(bytes[i++]);

            for (int c = 0; c < 5; c++)
            {
                int sn = bytes[i++];
                Splay sp = (Splay)bytes[i++];
                var list = new int[sn];
                for (int k = 0; k < sn; k++) list[k] = bytes[i++];
                p.Stacks[c].RestoreFromCode(list, sp);
            }

            int pscn = bytes[i++];
            p.ScorePile.Clear();
            for (int k = 0; k < pscn; k++) p.ScorePile.Add(bytes[i++]);

            int ac = bytes[i++];
            p.AgeAchievements.Clear();
            for (int k = 0; k < ac; k++) p.AgeAchievements.Add(bytes[i++]);

            byte pSpecial = bytes[i++];
            p.SpecialAchievements.Clear();
            for (int s = 0; s < SpecialAchOrder.Length; s++)
                if ((pSpecial & (1 << s)) != 0)
                    p.SpecialAchievements.Add(SpecialAchOrder[s]);

            p.ScoredThisTurn = bytes[i++];
            p.TuckedThisTurn = bytes[i++];
        }

        return g;
    }

    private static void WriteIds(List<byte> w, IReadOnlyList<int> ids)
    {
        w.Add((byte)ids.Count);
        foreach (var id in ids) w.Add((byte)id);
    }

    private static void WriteU16(List<byte> w, ushort v)
    {
        w.Add((byte)(v & 0xFF));
        w.Add((byte)(v >> 8));
    }

    private static ushort ReadU16(byte[] b, ref int i)
    {
        ushort v = (ushort)(b[i] | (b[i + 1] << 8));
        i += 2;
        return v;
    }
}
