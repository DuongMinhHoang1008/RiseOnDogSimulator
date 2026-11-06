using System;
using System.Text;
using UnityEngine;

namespace DogSimulator.Networking
{
    public static class RoomCodeService
    {
        // Invite convenience only; a room code is not an authentication secret.
        public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        public const int MinimumLength = 6;
        public const int MaximumLength = 8;

        public static string Generate(int length = MaximumLength)
        {
            if (length < MinimumLength || length > MaximumLength)
                throw new ArgumentOutOfRangeException(nameof(length));

            var code = new StringBuilder(length);
            for (var i = 0; i < length; i++)
                code.Append(Alphabet[UnityEngine.Random.Range(0, Alphabet.Length)]);
            return code.ToString();
        }

        public static bool TryNormalize(string value, out string code)
        {
            code = (value ?? string.Empty).Trim().ToUpperInvariant();
            if (code.Length < MinimumLength || code.Length > MaximumLength)
                return false;
            foreach (var character in code)
                if (Alphabet.IndexOf(character) < 0)
                    return false;
            return true;
        }
    }
}
