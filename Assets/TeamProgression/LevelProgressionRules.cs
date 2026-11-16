using System;

namespace CampusCommons.TeamProgression
{
    // Pure, side-effect-free policy shared by UI previews and future integration tests.
    // Never use this client-side policy as authorization to grant XP or change account levels.
    public enum GameOutcome
    {
        QuizWin,
        QuizLoss,
        QuizDraw,
        EscapeSuccess,
        EscapeFailure
    }

    public static class LevelProgressionRules
    {
        public const int MinLevel = 1;
        public const int MaxLevel = 15;
        public const int AuraUnlockLevel = 5;
        public const int CapeUnlockLevel = 10;
        public const int SpecialEffectUnlockLevel = 15;

        public static int RewardFor(GameOutcome outcome)
        {
            switch (outcome)
            {
                case GameOutcome.QuizWin:
                case GameOutcome.EscapeSuccess:
                    return 100;
                case GameOutcome.QuizLoss:
                case GameOutcome.EscapeFailure:
                    return 30;
                case GameOutcome.QuizDraw:
                    return 50;
                default:
                    throw new ArgumentOutOfRangeException(nameof(outcome));
            }
        }

        // Total XP required to REACH a level, not XP needed within that level.
        // Lv.1=0, Lv.5=1000, Lv.10=4500, Lv.15=10500.
        public static long TotalXpRequiredForLevel(int level)
        {
            EnsureValidLevel(level);
            return 50L * (level - 1) * level;
        }

        // Pure threshold calculation; does not change the server's persisted level.
        public static int LevelForTotalXp(long totalXp)
        {
            if (totalXp < 0)
                throw new ArgumentOutOfRangeException(nameof(totalXp));

            for (int level = MaxLevel; level > MinLevel; level--)
            {
                if (totalXp >= TotalXpRequiredForLevel(level))
                    return level;
            }
            return MinLevel;
        }

        // Existing levels are independent in the account DB. Never reduce a stored level.
        // Only a trusted, verified server event may cause this proposed level transition.
        public static int LevelAfterVerifiedReward(int storedLevel, long verifiedTotalXp)
        {
            EnsureValidLevel(storedLevel);
            return Math.Max(storedLevel, LevelForTotalXp(verifiedTotalXp));
        }

        public static bool HasAura(int level)
        {
            EnsureValidLevel(level);
            return level >= AuraUnlockLevel;
        }

        public static bool HasCape(int level)
        {
            EnsureValidLevel(level);
            return level >= CapeUnlockLevel;
        }

        public static bool HasSpecialEffect(int level)
        {
            EnsureValidLevel(level);
            return level >= SpecialEffectUnlockLevel;
        }

        static void EnsureValidLevel(int level)
        {
            if (level < MinLevel || level > MaxLevel)
                throw new ArgumentOutOfRangeException(nameof(level));
        }
    }
}
