using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace STS2MultiplayerLimitBreak.Rooms
{
    internal static class RoomInviteCodec
    {
        public const uint AppId = 2868840;
        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        public static bool IsLobbyId(ulong id)
        {
            return (id >> 56) == 1 && ((id >> 52) & 15) == 8 &&
                   (((id >> 32) & 0xFFFFF) & 0x40000) != 0 && (uint)id != 0;
        }

        public static string Encode(ulong lobbyId)
        {
            if (!IsLobbyId(lobbyId)) throw new ArgumentOutOfRangeException(nameof(lobbyId));
            Span<byte> bytes = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(bytes, lobbyId);
            var checksum = SHA256.HashData(bytes);
            var value = lobbyId;
            Span<char> text = stackalloc char[15];
            for (var i = 12; i >= 0; i--)
            {
                text[i] = Alphabet[(int)(value & 31)];
                value >>= 5;
            }
            text[13] = Alphabet[checksum[0] >> 3];
            text[14] = Alphabet[((checksum[0] & 7) << 2) | (checksum[1] >> 6)];
            return $"MLB1-{new string(text[..5])}-{new string(text[5..10])}-{new string(text[10..])}";
        }

        public static string Link(ulong lobbyId, ulong ownerId)
        {
            if (!IsLobbyId(lobbyId)) throw new ArgumentOutOfRangeException(nameof(lobbyId));
            if (ownerId == 0) throw new ArgumentOutOfRangeException(nameof(ownerId));
            return $"steam://joinlobby/{AppId}/{lobbyId}/{ownerId}";
        }

        public static bool TryDecode(string? input, out ulong lobbyId)
        {
            lobbyId = 0;
            if (string.IsNullOrWhiteSpace(input) || input.Length > 256) return false;
            var text = input.Trim();
            if (text.StartsWith("steam://", StringComparison.OrdinalIgnoreCase))
            {
                if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) ||
                    !uri.Host.Equals("joinlobby", StringComparison.OrdinalIgnoreCase) ||
                    uri.UserInfo.Length != 0 || !uri.IsDefaultPort || uri.Query.Length != 0 || uri.Fragment.Length != 0)
                    return false;
                var parts = uri.AbsolutePath.Split('/');
                if (parts.Length is not (3 or 4) || parts[0] != "" || parts[1] != AppId.ToString(CultureInfo.InvariantCulture) ||
                    !ulong.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var id) || !IsLobbyId(id))
                    return false;
                if (parts.Length == 4 && (!ulong.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var owner) || owner == 0))
                    return false;
                lobbyId = id;
                return true;
            }

            text = text.ToUpperInvariant();
            if (!text.StartsWith("MLB1-", StringComparison.Ordinal)) return false;
            var payload = text[5..].Replace("-", "", StringComparison.Ordinal);
            if (payload.Length != 15) return false;
            ulong decoded = 0;
            for (var i = 0; i < 13; i++)
            {
                var digit = Alphabet.IndexOf(payload[i]);
                if (digit < 0 || i == 0 && digit > 15) return false;
                decoded = (decoded << 5) | (uint)digit;
            }
            if (!IsLobbyId(decoded) || Encode(decoded)[5..].Replace("-", "", StringComparison.Ordinal) != payload)
                return false;
            lobbyId = decoded;
            return true;
        }

        public static string NormalizeMatchCode(string code)
        {
            return code.Trim().Normalize(NormalizationForm.FormC);
        }

        public static string MatchTag(string code)
        {
            var bytes = Encoding.UTF8.GetBytes($"MLB:match:1:{NormalizeMatchCode(code)}");
            return "mlb_group_" + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        }
    }
}
