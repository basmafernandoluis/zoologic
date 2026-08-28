namespace Zoologic.Core
{
    public static class LevelConfig
    {
        public static int GetGridSize(int level)
        {
            if (level <= 3) return 4;
            if (level <= 15) return 5;
            if (level <= 40) return 6;
            if (level <= 80) return 7;
            return 8;
        }

        public static int GetTargetDifficulty(int level)
        {
            if (level % 5 == 0) return 1;
            if (level % 3 == 0) return 3;
            return 2;
        }
    }
}
