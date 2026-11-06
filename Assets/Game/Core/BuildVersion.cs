namespace Game.Core
{
    public static class BuildVersion
    {
        public const string Unknown = "dev";

        public static string Format(int commitCount, string shortHash)
        {
            if (commitCount <= 0)
            {
                return Unknown;
            }

            return string.IsNullOrEmpty(shortHash) ? $"b{commitCount}" : $"b{commitCount}-{shortHash}";
        }
    }
}
