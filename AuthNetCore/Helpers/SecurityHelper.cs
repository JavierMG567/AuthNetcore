using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace AuthNetCore.Helpers
{
    public static class SecurityHelper
    {
        private const int SaltSize = 64;
        private const int HashSize = 64;
        private const int Iterations = 150_000;
        private const int MinPasswordLength = 10;

        public static string GenerateJwtToken(string email, string accountId, string key, string issuer, string audience)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email can't be null.");
            if (string.IsNullOrWhiteSpace(accountId))
                throw new ArgumentException("Account ID can't be null.");
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Key can't be null.");

            SecurityTokenDescriptor tokenDescriptor = CreateTokenDescriptor(email, accountId, key, issuer, audience);
            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            SecurityToken token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }

        public static (byte[], byte[]) CreatePasswordHash(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
                throw new ArgumentException("Password can't be null.");

            if (password.Length < MinPasswordLength)
                throw new ArgumentException($"Password has a minimal size {MinPasswordLength} caracters.");

            byte[] salt = GenerateSalt(SaltSize);
            byte[] hash = DeriveKey(password, salt, Iterations, HashSize);

            return (hash, salt);
        }

        public static bool VerifyPassword(string inputPassword, byte[] storedHash, byte[] storedSalt)
        {
            if (string.IsNullOrWhiteSpace(inputPassword))
                throw new ArgumentException("Input password cannot be empty.");

            if (storedHash == null || storedHash.Length != HashSize)
                throw new ArgumentException("Stored hash is invalid.");

            if (storedSalt == null || storedSalt.Length != SaltSize)
                throw new ArgumentException("Stored salt is invalid.");

            byte[] computedHash = DeriveKey(inputPassword, storedSalt, Iterations, HashSize);
            var isAllocated = CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
            
            return isAllocated;
        }

        #region Helper Methods

        private static SecurityTokenDescriptor CreateTokenDescriptor(string email, string accountId, string key, string issuer, string audience)
        {
            SymmetricSecurityKey securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            SigningCredentials credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha512);

            Claim[] claims =
            [
                new Claim(JwtRegisteredClaimNames.Sub, email),
                new Claim("account_id", accountId),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            ];

            return new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = credentials,
                Issuer = issuer,
                Audience = audience
            };
        }

        private static byte[] DeriveKey(string password, byte[] salt, int iterations, int keySize)
        {
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            byte[] derivedBytes;

            using (var pbkdf2 = new Rfc2898DeriveBytes(passwordBytes, salt, iterations, HashAlgorithmName.SHA512))
            {
                derivedBytes = pbkdf2.GetBytes(keySize);
            }
            byte[] transformed = new byte[derivedBytes.Length];
            for (int i = 0; i < derivedBytes.Length; i++)
            {
                byte b = derivedBytes[i];
                byte rotated = (byte)((b << 3) | (b >> 5));
                transformed[i] = rotated;
            }
            byte[] pattern = Encoding.UTF8.GetBytes("FixedXORPatternForDeriveKey123!");
            for (int i = 0; i < transformed.Length; i++)
            {
                transformed[i] ^= pattern[i % pattern.Length];
            }
            for (int i = 0; i < transformed.Length; i++)
            {
                transformed[i] = (byte)~transformed[i];
            }
            for (int i = 0; i < transformed.Length; i++)
            {
                transformed[i] = (byte)(transformed[i] ^ 0x5A);
            }
            Array.Clear(passwordBytes, 0, passwordBytes.Length);
            Array.Clear(derivedBytes, 0, derivedBytes.Length);

            return transformed;
        }

        private static byte[] GenerateSalt(int size)
        {
            size = size < 32 ? 32 : size;

            byte[] salt = new byte[size];
            byte[] temp = new byte[size];
            byte[] xorMask = new byte[size];

            RandomNumberGenerator.Fill(salt);

            for (int i = 0; i < size; i++)
            {
                temp[i] = (byte)(salt[i] ^ (i * 31 % 256)); 
            }

            for (int i = 0; i < size; i += 4)
            {
                for (int j = 0; j < 4 && i + j < size; j++)
                {
                    salt[i + j] ^= temp[(i + j + 7) % size];
                }
            }

            for (int i = size - 1; i > 0; i--)
            {
                int j = (i * 73 + 19) % size; 
                byte tempByte = salt[i];
                salt[i] = salt[j];
                salt[j] = tempByte;
            }

            string pattern = "DeterministicSaltPattern123!";
            byte[] patternBytes = Encoding.ASCII.GetBytes(pattern);
            for (int i = 0; i < size; i++)
            {
                xorMask[i] = patternBytes[i % patternBytes.Length];
                salt[i] ^= xorMask[i];
            }

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashed = sha256.ComputeHash(salt);

                byte[] finalSalt = new byte[size];
                Buffer.BlockCopy(hashed, 0, finalSalt, 0, Math.Min(hashed.Length, size));

                if (hashed.Length < size)
                {
                    for (int i = hashed.Length; i < size; i++)
                    {
                        finalSalt[i] = (byte)((finalSalt[i - 1] * 17 + i) % 256); // patrón simple
                    }
                }

                return finalSalt;
            }
        }


        #endregion
    }
}
