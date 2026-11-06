using DogSimulator.Networking;
using DogSimulator.Player;
using NUnit.Framework;

namespace DogSimulator.Tests
{
    public sealed class GuestAndRoomTests
    {
        [TestCase("  Cún Văn Phòng  ", "Cún Văn Phòng")]
        [TestCase("Dog 🐕", "Dog 🐕")]
        [TestCase("abcdefghijklmnop", "abcdefghijklmnop")]
        public void GuestNameAcceptsBoundedUnicodeAndTrimsWhitespace(string value, string expected)
        {
            Assert.That(GuestProfile.TryNormalizeName(value, out var name, out var error), Is.True, error);
            Assert.That(name, Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase(" ")]
        [TestCase("A")]
        [TestCase("abcdefghijklmnopq")]
        [TestCase("Dog\n")]
        [TestCase("Dog\tName")]
        [TestCase("Dog\u200bName")]
        public void GuestNameRejectsInvalidPayload(string value)
        {
            Assert.That(GuestProfile.TryNormalizeName(value, out var name, out var error), Is.False);
            Assert.That(name, Is.Empty);
            Assert.That(error, Is.Not.Empty);
        }

        [Test]
        public void GuestNameRejectsUnpairedSurrogatesWithoutTestArgumentEncoding()
        {
            Assert.That(GuestProfile.TryNormalizeName("Dog" + (char)0xd800, out _, out _), Is.False);
            Assert.That(GuestProfile.TryNormalizeName("Dog" + (char)0xdc00, out _, out _), Is.False);
        }

        [TestCase("  abcd23  ", "ABCD23")]
        [TestCase("EFGH4567", "EFGH4567")]
        public void InviteCodeNormalizesWithoutChangingItsMeaning(string value, string expected)
        {
            Assert.That(RoomCodeService.TryNormalize(value, out var code), Is.True);
            Assert.That(code, Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("ABCDE")]
        [TestCase("ABCDEFGH2")]
        [TestCase("ABC I2")]
        [TestCase("ABC012")]
        [TestCase("ABCＯ23")]
        [TestCase("ABC\n23")]
        public void InviteCodeRejectsInvalidOrAmbiguousCharacters(string value)
            => Assert.That(RoomCodeService.TryNormalize(value, out _), Is.False);

        [Test]
        public void GeneratedCodesFitTheInviteContract()
        {
            for (var length = 6; length <= 8; length++)
                for (var i = 0; i < 100; i++)
                {
                    var generated = RoomCodeService.Generate(length);
                    Assert.That(generated.Length, Is.EqualTo(length));
                    Assert.That(RoomCodeService.TryNormalize(generated, out var code), Is.True);
                    Assert.That(code, Is.EqualTo(generated));
                }
            Assert.Throws<System.ArgumentOutOfRangeException>(() => RoomCodeService.Generate(5));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => RoomCodeService.Generate(9));
        }
    }
}
