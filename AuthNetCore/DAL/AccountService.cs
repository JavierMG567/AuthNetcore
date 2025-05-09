using AuthNetCore.DAL.IDAL;
using AuthNetCore.Data.Access;
using AuthNetCore.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using AuthNetCore.Data.Models.DTOs;
using AuthNetCore.Data.Models.Entities;
using AuthNetCore.Data.Models.EntityFrameworkModels;

namespace AuthNetCore.DAL
{
    public partial class AccountService : IAccountService
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

        public async Task<(AccountDto, string)> AccountAuthenticateAsync(AccountLogin accountLogin)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(accountLogin.Email))
                    throw new Exception("Email is required to login into the account.");

                if (string.IsNullOrWhiteSpace(accountLogin.Password))
                    throw new Exception("Password is required to login into the account.");

                Account? account = await GetAccountByEmailAsync(accountLogin.Email);

                if (account == null)
                    throw new Exception("Account does not exists, address not found.");

                await UnlockAccountAsync(account);

                AccountAuth? auth = await GetAccountAuthByAccountIdAsync(account.Id);

                if (!await ValidatePasswordStashAsync(auth, accountLogin, account))
                    throw new Exception("Password is invalid, please verify your password.");

                return await HandleSuccessfulLoginAsync(account);
            }
            catch (Exception ex)
            {
                throw new Exception("Error during account authentication: " + ex.Message);
            }
        }

        public async Task<(AccountDto?, string)> AccountRegisterAsync(AccountRegistration accountRegistration)
        {
            using var transaction = await _authNetCoreDbContext.Database.BeginTransactionAsync();
            try
            {
                Account account = await CreateAccountAsync(accountRegistration);
                string token = await StablishAccountCredentialsAsync(accountRegistration.Password, account.Id, account);
                await transaction.CommitAsync();

                return (ReturnAccountDtoObject(account), token);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception("Error during account registration: " + ex.Message);
            }
        }

        public async Task AccountDeleteAsync(string tokenString)
        {
            using var transaction = await _authNetCoreDbContext.Database.BeginTransactionAsync();
            try
            {
                int accountId = GetAccountIdFromToken(tokenString);

                Account account = await _authNetCoreDbContext.Accounts
                                           .FindAsync(accountId) ?? throw new Exception("Account not found.");

                await RemoveAccountCredentialsAsync(accountId);
                _authNetCoreDbContext.Accounts.Remove(account);
                await _authNetCoreDbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception("Error during account deletion: " + ex.Message);
            }
        }

        public async Task<bool> RevokeTokenAsync(string token)
        {
            try
            {
                AccountSession? session = await _authNetCoreDbContext
                    .AccountSessions
                    .FirstOrDefaultAsync(acc => acc.Token == token);

                if (session == null || session.IsRevoked) return false;

                session.IsRevoked = true;
                await AddingTokenToBlackListAsync(session.AccountId, token);
                await _authNetCoreDbContext.SaveChangesAsync();

                return true;
            }
            catch (Exception ex)
            {
                throw new Exception("Error during token revocation: " + ex.Message);
            }
        }

        #region AccountService Helper Methods

        private async Task AddingTokenToBlackListAsync(int id, string token)
        {
            BlackListToken blackListTokenItem = new BlackListToken
            {
                AccountId = id,
                Token = token,
                RevokeTokenDateTime = DateTime.Now
            };
            await _authNetCoreDbContext.BlackListToken.AddAsync(blackListTokenItem);
        }

        private async Task<Account> CreateAccountAsync(AccountRegistration accountRegistration)
        {
            try
            {
                ValidateCredentialsNotNullOrEmpty(accountRegistration);
                ValidateAccountDataNotNullOrEmpty(accountRegistration);
                Account? account = await GetAccountByEmailAsync(accountRegistration.Email);

                if (account != null)
                    throw new Exception("Account already exists.");

                account = new Account
                {
                    Name = accountRegistration.Name,
                    LastName = accountRegistration.LastName,
                    Email = accountRegistration.Email,
                    BirthDate = accountRegistration.BirthDate
                };

                await _authNetCoreDbContext.Accounts.AddAsync(account);
                await _authNetCoreDbContext.SaveChangesAsync();


                return account;
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        private async Task CreateAccountAuthAsync(string password, int accountId)
        {
            try
            {
                var (passwordHash, passwordSalt) = SecurityHelper.CreatePasswordHash(password);
                AccountAuth accountAuth = new AccountAuth
                {
                    AccountId = accountId,
                    PasswordHash = passwordHash,
                    PasswordSalt = passwordSalt
                };
                await _authNetCoreDbContext.AccountAuth.AddAsync(accountAuth);
                await _authNetCoreDbContext.SaveChangesAsync();
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        private async Task CreateAccountSessionAsync(int accountId, string token)
        {
            try
            {
                AccountSession accountSession = new AccountSession
                {
                    AccountId = accountId,
                    Token = token,
                    IsRevoked = false,
                    Created = DateTime.Now
                };
                await _authNetCoreDbContext.AccountSessions.AddAsync(accountSession);
                await _authNetCoreDbContext.SaveChangesAsync();
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }

        private string GenerateJwtToken(Account account)
        {
            return SecurityHelper.GenerateJwtToken(
                account.Email,
                account.Id.ToString(),
                _jwtSettings.Key,
                _jwtSettings.Issuer,
                _jwtSettings.Audience
            );
        }

        private int GetAccountIdFromToken(string tokenString)
        {
            try
            {
                JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
                byte[] key = Encoding.UTF8.GetBytes(_jwtSettings.Key);

                TokenValidationParameters validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _jwtSettings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = _jwtSettings.Audience,
                };

                ClaimsPrincipal principal = tokenHandler.ValidateToken(tokenString, validationParameters, out SecurityToken validatedToken);
                Claim accountIdClaim = principal.Claims.FirstOrDefault(c => c.Type == "account_id") ?? throw new Exception("Token does not contain account_id.");

                return Convert.ToInt32(accountIdClaim.Value);
            }
            catch (Exception ex)
            {
                throw new Exception("Invalid token: " + ex.Message);
            }
        }

        private async Task HandleFailedLoginAttemptAsync(Account account)
        {
            account.FailedLoginAttempts++;
            if (account.FailedLoginAttempts >= 3)
            {
                await LockAccountAsync(account);
            }
            await _authNetCoreDbContext.SaveChangesAsync();
        }

        private async Task<(AccountDto, string)> HandleSuccessfulLoginAsync(Account account)
        {
            try
            {
                string token = GenerateJwtToken(account);
                await CreateAccountSessionAsync(account.Id, token);
                await ResetFailedLoginAttempts(account);

                return (ReturnAccountDtoObject(account), token);
            }
            catch (Exception)
            {
                throw new Exception("An error occurred while handling a successful login.");
            }
        }

        private async Task LockAccountAsync(Account account)
        {
            AccountSession? session = await _authNetCoreDbContext.AccountSessions
                .Where(a => a.AccountId == account.Id && !a.IsRevoked)
                .FirstOrDefaultAsync();

            if (session != null)
            {
                await AddingTokenToBlackListAsync(session.Id, session.Token);
                await _authNetCoreDbContext.SaveChangesAsync();
            }

            account.IsLocked = true;
            account.LockoutEnd = DateTime.Now.AddMinutes(12);
        }

        private async Task<bool> ValidatePasswordStashAsync(AccountAuth? auth, AccountLogin accountLogin, Account account)
        {
            try
            {
                if (!SecurityHelper.VerifyPassword(
                        accountLogin.Password,
                        auth.PasswordHash,
                        auth.PasswordSalt)
                   )
                {
                    await HandleFailedLoginAttemptAsync(account);
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        private async Task ResetFailedLoginAttempts(Account account)
        {
            account.FailedLoginAttempts = 0;
            _authNetCoreDbContext.Update(account);
            await _authNetCoreDbContext.SaveChangesAsync();
        }

        private AccountDto ReturnAccountDtoObject(Account account)
        {
            return new AccountDto
            {
                FullName = $"{account.Name} {account.LastName}",
                Email = account.Email,
                BirthDate = account.BirthDate,
                IsLocked = account.IsLocked
            };
        }

        private async Task RemoveAccountCredentialsAsync(int id)
        {
            try
            {
                List<AccountSession> sessions = await GetAccountSessionById(id);
                RemoveAccountSessions(sessions);
                AccountAuth? accountAuth = await GetAccountAuthByAccountIdAsync(id);
                if (accountAuth == null) return;
                RemoveAccountAuth(accountAuth);
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }
        
        private async Task<bool> SenderEMailRecipentAsync(string email, int id, string token)
        {
            if (!EmailServiceHelper.SendPasswordRecoveryEmail(email)) return false;
            await AddingTokenToBlackListAsync(id, token);
            await _authNetCoreDbContext.SaveChangesAsync();
            return true;
        }

        private async Task<string> StablishAccountCredentialsAsync(string password, int accountId, Account accountDto)
        {
            try
            {
                await CreateAccountAuthAsync(password, accountId);
                string token = GenerateJwtToken(accountDto);
                await CreateAccountSessionAsync(accountId, token);

                return token;
            }
            catch (Exception ex)
            {
                throw new Exception(ex.Message);
            }
        }

        private async Task UnlockAccountAsync(Account account)
        {
            if (account.IsLocked && account.LockoutEnd.HasValue)
            {
                if (DateTime.UtcNow >= account.LockoutEnd.Value)
                {
                    account.IsLocked = false;
                    account.FailedLoginAttempts = 0;
                    account.LockoutEnd = null;
                    _authNetCoreDbContext.Accounts.Update(account);
                    await _authNetCoreDbContext.SaveChangesAsync();
                }
                else
                {
                    TimeSpan remainingLockTime = account.LockoutEnd.Value - DateTime.UtcNow;
                    throw new Exception($"Account is locked. Please wait {remainingLockTime.Minutes} minutes and {remainingLockTime.Seconds} seconds before trying again.");
                }
            }
        }

        #endregion
    }
}
