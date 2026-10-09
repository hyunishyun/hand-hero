using UnityEngine;

namespace HandHero.Core
{
    // Which game moment plays which sound (D14). The MonoBehaviours only forward
    // phase changes and hero events here and play what comes back.
    public static class SfxCues
    {
        public static SfxId ForMatchPhase(MatchPhase phase, MatchSide roundWinner, MatchSide matchWinner)
        {
            switch (phase)
            {
                case MatchPhase.Fight: return SfxId.Fight;
                case MatchPhase.RoundEnd: return roundWinner == MatchSide.Player ? SfxId.IslandCleared : SfxId.None;
                case MatchPhase.MatchEnd: return matchWinner == MatchSide.Player ? SfxId.Victory : SfxId.Defeat;
                default: return SfxId.None;
            }
        }

        public static SfxId ForRunPhase(RunPhase phase)
        {
            switch (phase)
            {
                case RunPhase.Island: return SfxId.Fight;
                case RunPhase.IslandCleared: return SfxId.IslandCleared;
                case RunPhase.OpenChest: return SfxId.ChestOpen;
                case RunPhase.Victory: return SfxId.Victory;
                case RunPhase.Defeat: return SfxId.Defeat;
                default: return SfxId.None;
            }
        }

        // Quick Match countdown or the intro before a run island.
        public static bool IsCountdown(MatchPhase match, RunPhase run)
        {
            return match == MatchPhase.Countdown || (match == MatchPhase.Run && run == RunPhase.Intro);
        }

        // The player hears their own hits taken; hits on bots are confirmed by HitDealt at the shot.
        public static SfxId ForHeroDamaged(HeroTeam team) => team == HeroTeam.Player ? SfxId.HitTaken : SfxId.None;

        // A player death is followed by a revive or DEFEAT, which have their own sounds.
        public static SfxId ForHeroDied(HeroTeam team) => team == HeroTeam.Bot ? SfxId.BotDown : SfxId.None;

        public static SfxId ForShot(HeroTeam team, bool charged)
        {
            if (team != HeroTeam.Player) return SfxId.EnemyFire;
            return charged ? SfxId.ChargeRelease : SfxId.BeamFire;
        }
    }

    // One tick per whole second left in a countdown: 3, 2, 1. A hitch that skips
    // a second ticks once, not in a burst.
    public class CountdownTicker
    {
        private int _last = int.MaxValue;

        public void Reset() => _last = int.MaxValue;

        public bool Step(float remaining)
        {
            int second = Mathf.CeilToInt(remaining);
            if (second <= 0 || second >= _last) return false;
            _last = second;
            return true;
        }
    }

    // Drops a repeat of the same event inside minInterval seconds (several hits
    // in one frame, a bot burst), so stacked copies never get loud.
    public class SfxRateLimiter
    {
        private readonly float _minInterval;
        // Indexed by SfxId: no dictionary lookups or enum boxing per sound.
        private readonly float[] _last;

        public SfxRateLimiter(float minInterval)
        {
            _minInterval = minInterval;
            _last = new float[System.Enum.GetValues(typeof(SfxId)).Length];
            for (int i = 0; i < _last.Length; i++) _last[i] = float.NegativeInfinity;
        }

        public bool Allow(SfxId id, float time)
        {
            int i = (int)id;
            if (i < 0 || i >= _last.Length) return false;
            if (time - _last[i] < _minInterval) return false;
            _last[i] = time;
            return true;
        }
    }
}
