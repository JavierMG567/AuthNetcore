using System;
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
        private const int MinPasswordLength = 12;

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

        public static (byte[] Hash, byte[] Salt) CreatePasswordHash(string password)
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
            return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
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
            byte[] entropy = new byte[salt.Length];
            RandomNumberGenerator.Fill(entropy);

            for (int i = 0; i < salt.Length; i++)
                salt[i] ^= entropy[i];

            using (Rfc2898DeriveBytes pbkdf2 = new Rfc2898DeriveBytes(passwordBytes, salt, iterations, HashAlgorithmName.SHA512))
            {
                byte[] rawKey = pbkdf2.GetBytes(keySize);
                byte[] mask = new byte[keySize];
                RandomNumberGenerator.Fill(mask);

                for (int i = 0; i < keySize; i++)
                    rawKey[i] ^= mask[i];

                using (SHA512 sha512 = SHA512.Create())
                {
                    byte[] finalKey = sha512.ComputeHash(rawKey);
                    return finalKey.Take(keySize).ToArray();
                }
            }
        }

        private static byte[] GenerateSalt(int size)
        {
            if (size < 32)
                throw new ArgumentException("Salt size debe ser al menos 32 bytes.");

            byte[] rawSalt = new byte[size];
            RandomNumberGenerator.Fill(rawSalt);

            byte[] perturbation = new byte[size];
            RandomNumberGenerator.Fill(perturbation);

            for (int i = 0; i < size; i++)
                rawSalt[i] ^= perturbation[i];

            for (int i = size - 1; i > 0; i--)
            {
                int j = RandomNumberGenerator.GetInt32(i + 1);
                (rawSalt[i], rawSalt[j]) = (rawSalt[j], rawSalt[i]);
            }

            using (SHA256 sha256 = SHA256.Create())
            {
                rawSalt = sha256.ComputeHash(rawSalt);
            }

            if (rawSalt.Length < size)
            {
                byte[] extended = new byte[size];
                Buffer.BlockCopy(rawSalt, 0, extended, 0, rawSalt.Length);
                RandomNumberGenerator.Fill(extended.AsSpan(rawSalt.Length));
                return extended;
            }

            return rawSalt.Take(size).ToArray();
        }

        #endregion
    }
}
