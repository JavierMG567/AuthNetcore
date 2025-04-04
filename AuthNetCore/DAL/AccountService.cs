using AuthNetCore.DAL.IDAL;
using AuthNetCore.Data.Access;
using AuthNetCore.Data.Models.DTos;
using AuthNetCore.Data.Models.EModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace AuthNetCore.DAL
{
    public class AccountService : IAccountService
    {
        private readonly AuthNetCoreDbContext _authNetCoreDbContext;
        private readonly JwtSettings _jwtSettings;
        public AccountService(
            AuthNetCoreDbContext authNetCoreDbContext,
            IOptions<JwtSettings> jwtSettings)
        {
            _authNetCoreDbContext = authNetCoreDbContext;
            _jwtSettings = jwtSettings.Value;
        }

        public async Task<AccountDto> AccountAuthenticateAsync(AccountLogin accountLogin)
        {
            AccountDto? account = await _authNetCoreDbContext.Account.FirstOrDefaultAsync(a => a.Email == accountLogin.Email);
            if (account == null) return null;
            if (account.IsLocked)
            {
                return null;
            }
            AccountAuth? auth = await _authNetCoreDbContext.AccountAuth.FirstOrDefaultAsync(a => a.AccountId == account.Id);
            if (auth == null) return null;
            bool isValidPassword = VerifyPassword(accountLogin.Password, auth.PasswordHash, auth.PasswordSalt);
            if (isValidPassword)
            {
                var token = GenerateJwtToken(account);
                var accountSession = new AccountSessionsDto
                {
                    AccountId = account.Id,
                    Token = token,
                    IsRevoked = false
                };

                await _authNetCoreDbContext.AccountSessions.AddAsync(accountSession);
                await _authNetCoreDbContext.SaveChangesAsync();
                account.FailedLoginAttempts = 0;
                await _authNetCoreDbContext.SaveChangesAsync();
            }
            else
            {
                account.FailedLoginAttempts++;
                if (account.FailedLoginAttempts >= 3)
                {
                    var session = await _authNetCoreDbContext.AccountSessions
                                  .Where(a => a.AccountId == account.Id && !a.IsRevoked)  
                                  .FirstOrDefaultAsync();
                    if (session != null)
                    {
                        BlackListTokenDto blackListTokenItem = new BlackListTokenDto
                        {
                            AccountId = session.Id,
                            Token = session.Token,
                        };
                        _authNetCoreDbContext.BlackListToken.Add(blackListTokenItem);
                    }
                    account.IsLocked = true;
                }
                await _authNetCoreDbContext.SaveChangesAsync();
            }
            return account;
        }

        public async Task AccountDeleteAsync(string tokenString)
        {
            using (var transaction = await _authNetCoreDbContext.Database.BeginTransactionAsync())
            {
                try
                {
                    var tokenHandler = new JwtSecurityTokenHandler();
                    var token = tokenHandler.ReadJwtToken(tokenString);
                    var accountIdClaim = token.Claims.FirstOrDefault(acc => acc.Type == "account_id");
                    if (accountIdClaim == null) throw new Exception();
                    var accountId = Convert.ToInt32(accountIdClaim.Value);
                    var account = await _authNetCoreDbContext.Account.FindAsync(accountId);
                    if (account == null) throw new Exception();
                    _authNetCoreDbContext.Account.Remove(account);
                    await _authNetCoreDbContext.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    throw new Exception();
                }   
            }
        }

        public async Task<AccountDto> AccountRegisterAsync(AccountRegistration accountRegistration)
        {
            try
            {
                AccountDto accountDetails = new AccountDto
                {
                    Id = accountRegistration.Id,
                    Name = accountRegistration.Name,
                    LastName = accountRegistration.LastName,
                    Email = accountRegistration.Email,
                    BirthDate = accountRegistration.BirthDate,
                };
                await _authNetCoreDbContext.Account.AddAsync(accountDetails);
                
                var (passwordHash, passwordSalt) = CreatePasswordHash(accountRegistration.Password);
                AccountAuth accountAuth = new AccountAuth
                {
                    AccountId = accountRegistration.Id,
                    PasswordHash = passwordHash,
                    PasswordSalt = passwordSalt
                };
                await _authNetCoreDbContext.AccountAuth.AddAsync(accountAuth);

                var token = GenerateJwtToken(accountDetails);
                var accountSession = new AccountSessionsDto
                {
                    AccountId = accountRegistration.Id,
                    Token = token,
                    IsRevoked = false
                };
                await _authNetCoreDbContext.AccountSessions.AddAsync(accountSession);

                await _authNetCoreDbContext.SaveChangesAsync();
                return accountDetails;
            }
            catch(Exception)
            {
                throw new Exception();
            }
        }

        public async Task<bool> RevokeTokenAsync(string token)
        {
            var session = await _authNetCoreDbContext.AccountSessions.FirstOrDefaultAsync(acc => acc.Token == token);
            if (session == null || session.IsRevoked)
            {
                return false;
            }
            session.IsRevoked = true;
            await _authNetCoreDbContext.SaveChangesAsync();
            return true;
        }

        private string GenerateJwtToken(AccountDto account)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_jwtSettings.Key);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(
                [
                    new Claim(JwtRegisteredClaimNames.Sub, account.Email),
                    new Claim("account_id", account.Id.ToString())
                ]),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = _jwtSettings.Issuer,
                Audience = _jwtSettings.Audience
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private (byte[] hash, byte[] salt) CreatePasswordHash(string password)
        {
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("Password cannot be null or empty.", nameof(password));
            }
            var salt = GenerateSalt();
            var hash = GenerateHash(password, salt);
            return (hash, salt);
        }

        private byte[] GenerateHash(string password, byte[] salt)
        {
            using (var hmac = new System.Security.Cryptography.HMACSHA512(salt))
            {
                var passwordBytes = Encoding.UTF8.GetBytes(password);
                var hash = hmac.ComputeHash(passwordBytes);
                return hash;
            }
        }

        private byte[] GenerateSalt(int saltSize = 32)
        {
            using (var rng = new System.Security.Cryptography.RNGCryptoServiceProvider())
            {
                var salt = new byte[saltSize];
                rng.GetBytes(salt);  
                return salt;
            }
        }

        private bool VerifyPassword(string inputPassword, byte[] storedHash, byte[] storedSalt)
        {
            var hash = GenerateHash(inputPassword, storedSalt);
            return hash.SequenceEqual(storedHash);
        }
    }
}
