using System.Globalization;

namespace DogSimulator.Player
{
    public static class GuestProfile
    {
        public const int MinimumNameLength = 2;
        public const int MaximumNameLength = 16;

        public static bool TryNormalizeName(string value, out string nickname, out string error)
        {
            nickname = string.Empty;
            error = string.Empty;
            var candidate = value ?? string.Empty;
            // Reject before trimming: newlines/control characters are never accepted.
            for (var i = 0; i < candidate.Length; i++)
            {
                if (char.IsSurrogate(candidate[i]) && (!char.IsHighSurrogate(candidate[i])
                    || i + 1 >= candidate.Length || !char.IsLowSurrogate(candidate[i + 1])))
                {
                    error = "Nickname contains invalid Unicode.";
                    return false;
                }
                var category = CharUnicodeInfo.GetUnicodeCategory(candidate, i);
                if (category == UnicodeCategory.Control || category == UnicodeCategory.Format
                    || category == UnicodeCategory.LineSeparator || category == UnicodeCategory.ParagraphSeparator
                    || category == UnicodeCategory.Surrogate)
                {
                    error = "Nickname contains an unsupported character.";
                    return false;
                }
                if (char.IsHighSurrogate(candidate[i]) && i + 1 < candidate.Length
                    && char.IsLowSurrogate(candidate[i + 1])) i++;
            }

            candidate = candidate.Trim();
            // Use UTF-16 units, keeping the eventual Fusion NetworkString payload bounded.
            if (candidate.Length < MinimumNameLength || candidate.Length > MaximumNameLength)
            {
                error = "Enter a nickname with 2–16 characters.";
                return false;
            }
            nickname = candidate;
            return true;
        }
    }
}
