using AuthNetCore.DAL.IDAL;
using AuthNetCore.Data.Access;
using AuthNetCore.Data.Models.DTos;
using AuthNetCore.Data.Models.EModels;
using AuthNetCore.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

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

        public async Task<(AccountDto, string)> AccountAuthenticateAsync(AccountLogin accountLogin)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(accountLogin.Email))
                    throw new Exception("Email is required to login into the account.");

                if (string.IsNullOrWhiteSpace(accountLogin.Password))
                    throw new Exception("Password is required to login into the account.");
            
                AccountDto? account = await GetAccountByEmailAsync(accountLogin.Email);

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
                AccountDto account = await CreateAccountAsync(accountRegistration);
                string token = await StablishAccountCredentialsAsync(accountRegistration.Password, account.Id, account);
                await transaction.CommitAsync();
                return (account, token);
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

                AccountDto account = await _authNetCoreDbContext.Accounts
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

        public async Task<AccountDto> PasswordRecoveryAsync(string email)
        {
            try
            {
                var (accountId, accountDto) = await GetAccountDtoIdByEmail(email);
                string token = await GetAccountSessionTokenCompromisedAsync(accountId);
                if (!await SenderEMailRecipentAsync(email, accountId, token))
                    return new AccountDto();
                return accountDto;
            }
            catch (Exception ex)
            {
                throw new Exception("Error during password recovery: " + ex.Message);
            }
        }

        public async Task<bool> ResetPasswordAsync(AccountResetPassword accountResetPassword)
        {
            using var transaction = await _authNetCoreDbContext.Database.BeginTransactionAsync();
            try
            {
                if (accountResetPassword.email == null ||
                    accountResetPassword.password == null ||
                    accountResetPassword.passwordConfirmation == null)
                    return false;

                var (id, accountDto) = await GetAccountDtoIdByEmail(accountResetPassword.email);
                if (accountDto.Id == 0) return false;
                await RemoveAccountCredentialsAsync(id);
                await StablishAccountCredentialsAsync(accountResetPassword.passwordConfirmation, id, accountDto);

                await transaction.CommitAsync();
                return true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception("Error during password reset: " + ex.Message);
            }
        }

        public async Task<bool> RevokeTokenAsync(string token)
        {
            try
            {
                AccountSessionsDto? session = await _authNetCoreDbContext
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
            BlackListTokenDto blackListTokenItem = new BlackListTokenDto
            {
                AccountId = id,
                Token = token,
                RevokeTokenDateTime = DateTime.Now
            };
            await _authNetCoreDbContext.BlackListToken.AddAsync(blackListTokenItem);
        }

        private async Task<AccountDto> CreateAccountAsync(AccountRegistration accountRegistration)
        {
            try
            {
                ValidateCredentialsNotNullOrEmpty(accountRegistration);
                ValidateAccountDataNotNullOrEmpty(accountRegistration);
                AccountDto? account = await GetAccountByEmailAsync(accountRegistration.Email);

                if (account != null) 
                    throw new Exception("Account already exists.");
                
                account = new AccountDto
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

        private void ValidateCredentialsNotNullOrEmpty(AccountRegistration accountRegistration)
        {
            
            if (string.IsNullOrEmpty(accountRegistration.Email))
                throw new Exception("Email is required to create an account.");

            if (string.IsNullOrEmpty(accountRegistration.Password) || string.IsNullOrEmpty(accountRegistration.ConfirmPassword))
                throw new Exception("Password and Confirm password are required to create an account.");

            if (!string.Equals(accountRegistration.Password, accountRegistration.ConfirmPassword, StringComparison.OrdinalIgnoreCase))
                throw new Exception("Password and Confirm password are not the same.");
        }

        private void ValidateAccountDataNotNullOrEmpty(AccountRegistration accountRegistration)
        {
            if (string.IsNullOrEmpty(accountRegistration.Name))
                throw new Exception("Name is required to create an account.");

            if (string.IsNullOrEmpty(accountRegistration.LastName))
                throw new Exception("LastName is required to create an account.");
            
            if (accountRegistration.BirthDate == DateOnly.MinValue && accountRegistration.BirthDate > DateOnly.FromDateTime(DateTime.Now))
                throw new Exception("BirthDate must be greater than or equal to DateOnly.MinValue.");
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
                AccountSessionsDto accountSession = new AccountSessionsDto
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

        private string GenerateJwtToken(AccountDto account)
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

        private async Task<AccountAuth?> GetAccountAuthByAccountIdAsync(int accountId)
        {
            return await _authNetCoreDbContext.AccountAuth.FirstOrDefaultAsync(a => a.AccountId == accountId);
        }

        private async Task<AccountDto?> GetAccountByEmailAsync(string email)
        {
            try
            {
                return await _authNetCoreDbContext
                    .Accounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Email == email);
            }
            catch (Exception ex)
            {
                throw new Exception("Error fetching account by email: " + ex.Message);
            }
        }

        private async Task<(int, AccountDto)> GetAccountDtoIdByEmail(string email)
        {
            try
            {
                AccountDto? accountDto = await _authNetCoreDbContext
                    .Accounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(acc => acc.Email == email);

                if (accountDto == null) return (0, new AccountDto());
                return (accountDto.Id, accountDto);
            }
            catch (Exception ex)
            {
                throw new Exception("Error fetching account ID by email: " + ex.Message);
            }
        }

        private async Task<string> GetAccountSessionTokenCompromisedAsync(int id)
        {
            try
            {
                AccountSessionsDto? accountSessionData = await _authNetCoreDbContext
                    .AccountSessions
                    .AsNoTracking()
                    .FirstOrDefaultAsync(accSData => accSData.AccountId == id);

                if (accountSessionData == null) return string.Empty;
                return accountSessionData.Token;
            }
            catch (Exception ex)
            {
                throw new Exception("Error fetching compromised session token: " + ex.Message);
            }
        }

        private async Task<List<AccountSessionsDto>> GetAccountSessionById(int id)
        {
            List<AccountSessionsDto> sessions = await _authNetCoreDbContext
                .AccountSessions
                .AsNoTracking()
                .Where(s => s.AccountId == id)
                .ToListAsync(); 
            
            return sessions;
        }

        private async Task HandleFailedLoginAttemptAsync(AccountDto account)
        {
            account.FailedLoginAttempts++;
            if (account.FailedLoginAttempts >= 3)
            {
                await LockAccountAsync(account);
            }
            await _authNetCoreDbContext.SaveChangesAsync();
        }
        
        private async Task<(AccountDto, string)> HandleSuccessfulLoginAsync(AccountDto account)
        {
            try
            {
                string token = GenerateJwtToken(account);
                await CreateAccountSessionAsync(account.Id, token);
                await ResetFailedLoginAttempts(account);

                return (account, token);
            }
            catch (Exception)
            {
                throw new Exception("An error occurred while handling a successful login.");
            }
        }

        private async Task LockAccountAsync(AccountDto account)
        {
            AccountSessionsDto? session = await _authNetCoreDbContext.AccountSessions
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

        private async Task<bool> ValidatePasswordStashAsync(AccountAuth? auth, AccountLogin accountLogin, AccountDto account)
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

        private void RemoveAccountAuth(AccountAuth accountAuth)
        {
            _authNetCoreDbContext.AccountAuth.Remove(accountAuth);
        }

        private void RemoveAccountSessions(IEnumerable<AccountSessionsDto> accountSessions)
        {
            _authNetCoreDbContext.AccountSessions.RemoveRange(accountSessions);
        }

        private async Task ResetFailedLoginAttempts(AccountDto account)
        {
            account.FailedLoginAttempts = 0;
            _authNetCoreDbContext.Update(account);
            await _authNetCoreDbContext.SaveChangesAsync();
        }

        private async Task RemoveAccountCredentialsAsync(int id)
        {
            try
            {
                List<AccountSessionsDto> sessions = await GetAccountSessionById(id);
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

        private async Task<string> StablishAccountCredentialsAsync(string password, int accountId, AccountDto accountDto)
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

        private async Task UnlockAccountAsync(AccountDto account)
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

                throw new Exception("Account is locked, You should wait for 15 minutes before try again.");
            }
        }

        #endregion
    }
}
