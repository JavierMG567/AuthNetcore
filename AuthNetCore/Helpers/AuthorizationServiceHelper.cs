using AuthNetCore.Data.Models.Entities;
using AuthNetCore.Utilities.Globals;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AuthNetCore.Helpers
{
    public class AuthorizationServiceHelper
    {
        private readonly JwtSettings _jwtSettings;

        public AuthorizationServiceHelper(IOptions<JwtSettings> jwtSettings)
        {
            _jwtSettings = jwtSettings.Value;
        }

        public string Authenticate(string username, string password)
        {
            UserRoleModel user = ValidateUser(username, password);
            if (user == null || string.IsNullOrWhiteSpace(user.Username))
                return string.Empty;

            Claim[] claims = GetClaims(user);
            byte[] key = GetSecurityKey();
            SecurityTokenDescriptor tokenDescriptor = GetTokenDescriptor(claims, key);

            return GenerateToken(tokenDescriptor);
        }

        #region AuthorizationService Help Methods

        private static UserRoleModel ValidateUser(string username, string password)
        {
            if (username == null)
                return new UserRoleModel();
            if (password == null)
                return new UserRoleModel();
            if (username == Globals.UserAuthenticationName &&
                password == Globals.PasswordAuthenticationServiceShelter)
            {
                return new UserRoleModel
                {
                    Id = 1,
                    Username = Globals.UserAuthenticationName,
                    Role = "Admin"
                };
            }

            return new UserRoleModel(); 
        }

        private Claim[] GetClaims(UserRoleModel user)
        {
            return
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim("role", user.Role),
                new Claim("auth_id", user.Id.ToString())
            ];
        }

        private byte[] GetSecurityKey()
        {
            return Encoding.UTF8.GetBytes(_jwtSettings.Key);
        }

        private SecurityTokenDescriptor GetTokenDescriptor(Claim[] claims, byte[] key)
        {
            return new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                IssuedAt = DateTime.UtcNow,
                NotBefore = DateTime.UtcNow,
                Expires = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature
                ),
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience
            };
        }

        private string GenerateToken(SecurityTokenDescriptor descriptor)
        {
            if (descriptor == null)
            {
                return string.Empty;
            }
            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            SecurityToken token = tokenHandler.CreateToken(descriptor);
            if (token == null)
            {
                return string.Empty;
            }
            JwtSecurityToken? jwtToken = token as JwtSecurityToken;
            if (jwtToken == null)
            {
                return string.Empty;
            }
            string tokenString = tokenHandler.WriteToken(jwtToken);
            if (string.IsNullOrWhiteSpace(tokenString))
            {
                return string.Empty;
            }
            string trimmedToken = tokenString.Trim();
            if (trimmedToken.Length == 0)
            {
                return string.Empty;
            }

            return trimmedToken;
        }

        #endregion
    }
}
