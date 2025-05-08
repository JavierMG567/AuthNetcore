using AuthNetCore.DAL.IDAL;
using AuthNetCore.Data.Access;
using AuthNetCore.Data.Models.DTos;
using AuthNetCore.Data.Models.EModels;
using AuthNetCore.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;

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
            if (string.IsNullOrWhiteSpace(accountLogin.Email) || string.IsNullOrWhiteSpace(accountLogin.Password))
                return (new AccountDto(), string.Empty);

            AccountDto? account = await GetAccountByEmailAsync(accountLogin.Email);

            if (account == null)
                return (new AccountDto(), string.Empty);

            await UnlockAccountAsync(account);

            AccountAuth? auth = await GetAccountAuthByAccountIdAsync(account.Id);

            if (!await ValidatePasswordStashAsync(auth, accountLogin, account))
                return (new AccountDto(), string.Empty);

            return await HandleSuccessfulLoginAsync(account);
        }

        public async Task<(AccountDto?, string)> AccountRegisterAsync(AccountRegistration accountRegistration)
        {
            using var transaction = await _authNetCoreDbContext.Database.BeginTransactionAsync();
            try
            {
                AccountDto? account = await CreateAccountAsync(accountRegistration);
                if (account == null) return (new AccountDto(), string.Empty);
                string token = await StablishAccountCredentialsAsync(accountRegistration.Password, account.Id, account);
                await transaction.CommitAsync();
                
                return (account, token);
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw new Exception();
            }
        }

        public async Task AccountDeleteAsync(string tokenString)
        {
            using var transaction = await _authNetCoreDbContext.Database.BeginTransactionAsync();
            try
            {
                int accountId = GetAccountIdFromToken(tokenString);

                AccountDto account = await _authNetCoreDbContext
                    .Accounts
                    .FindAsync(accountId) ?? throw new Exception("Account not found.");

                await RemoveAccountCredentialsAsync(accountId);
                _authNetCoreDbContext.Accounts.Remove(account);
                await _authNetCoreDbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw new Exception();
            }
        }

        public async Task<AccountDto> PasswordRecoveryAsync(string email)
        {
            var (accountId, accountDto) = await GetAccountDtoIdByEmail(email);
            string token = await GetAccountSessionTokenCompromisedAsync(accountId);
            if (!await SenderEMailRecipentAsync(email, accountId, token))
                return new AccountDto();
            return accountDto;
        }

        public async Task<bool> ResetPasswordAsync(AccountResetPassword accountResetPassword)
        {
            using var transaction = await _authNetCoreDbContext.Database.BeginTransactionAsync();
            try
            {
                if (accountResetPassword.email == null ||
                    accountResetPassword.password == null ||
                    accountResetPassword.passwordConfirmation == null) return false;

                var (id, accountDto) = await GetAccountDtoIdByEmail(accountResetPassword.email);
                if (accountDto.Id == 0) return false;
                await RemoveAccountCredentialsAsync(id);
                await StablishAccountCredentialsAsync(accountResetPassword.passwordConfirmation, id, accountDto);

                return true;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                throw new Exception();
            }
        }

        public async Task<bool> RevokeTokenAsync(string token)
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

        private async Task<AccountDto?> CreateAccountAsync(AccountRegistration accountRegistration)
        {
            if (!ValidateCredentialsNotNullOrEmpty(accountRegistration))
                return new AccountDto();
            if (!ValidateAccountDataNotNullOrEmpty(accountRegistration))
                return new AccountDto();
            
            AccountDto? account = await GetAccountByEmailAsync(accountRegistration.Email);

            if (account == null)
            {
                account = new AccountDto
                {
                    Name = accountRegistration.Name,
                    LastName = accountRegistration.LastName,
                    Email = accountRegistration.Email,
                    BirthDate = accountRegistration.BirthDate
                };

                await _authNetCoreDbContext.Accounts.AddAsync(account);
                await _authNetCoreDbContext.SaveChangesAsync();
            }

            return account;
        }

        private bool ValidateCredentialsNotNullOrEmpty(AccountRegistration accountRegistration)
        {
            
            if (string.IsNullOrEmpty(accountRegistration.Email))
                return false;

            if (string.IsNullOrEmpty(accountRegistration.Password)
                || string.IsNullOrEmpty(accountRegistration.ConfirmPassword)
                && !string.Equals(accountRegistration.Password, accountRegistration.ConfirmPassword,
                    StringComparison.OrdinalIgnoreCase))
                return false;
            
            return true;
        }

        private bool ValidateAccountDataNotNullOrEmpty(AccountRegistration accountRegistration)
        {
            if (string.IsNullOrEmpty(accountRegistration.Name))
                return false;

            if (string.IsNullOrEmpty(accountRegistration.LastName))
                return false;
            
            if (accountRegistration.BirthDate == DateOnly.MinValue && accountRegistration.BirthDate > DateOnly.FromDateTime(DateTime.Now))
                return false;
            
            return true;
        }

        private async Task CreateAccountAuthAsync(string password, int accountId)
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

        private async Task CreateAccountSessionAsync(int accountId, string token)
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
            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            JwtSecurityToken token = tokenHandler.ReadJwtToken(tokenString);
            Claim accountIdClaim = token.Claims.FirstOrDefault(acc => acc.Type == "account_id") ?? throw new Exception("Invalid token.");
            return Convert.ToInt32(accountIdClaim.Value);
        }

        private async Task<AccountAuth?> GetAccountAuthByAccountIdAsync(int accountId)
        {
            return await _authNetCoreDbContext.AccountAuth.FirstOrDefaultAsync(a => a.AccountId == accountId);
        }

        private async Task<AccountDto?> GetAccountByEmailAsync(string email)
        {
            return await _authNetCoreDbContext
                .Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Email == email);
        }

        private async Task<(int, AccountDto)> GetAccountDtoIdByEmail(string email)
        {
            AccountDto? accountDto = await _authNetCoreDbContext
                .Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(acc => acc.Email == email);

            if (accountDto == null) return (0, new AccountDto());
            return (accountDto.Id, accountDto);
        }

        private async Task<string> GetAccountSessionTokenCompromisedAsync(int id)
        {
            AccountSessionsDto? accountSessionData = await _authNetCoreDbContext
                .AccountSessions
                .AsNoTracking()
                .FirstOrDefaultAsync(accSData => accSData.AccountId == id);
            
            if (accountSessionData == null) return string.Empty;
            return accountSessionData.Token;
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
            string token = GenerateJwtToken(account);
            await CreateAccountSessionAsync(account.Id, token);
            await ResetFailedLoginAttempts(account);

            return (account, token);
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
            List<AccountSessionsDto> sessions = await GetAccountSessionById(id);
            RemoveAccountSessions(sessions);
            
            AccountAuth? accountAuth = await GetAccountAuthByAccountIdAsync(id);
            if (accountAuth == null) return;
            
            RemoveAccountAuth(accountAuth);
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
            await CreateAccountAuthAsync(password, accountId);
            string token = GenerateJwtToken(accountDto);
            await CreateAccountSessionAsync(accountId, token);

            return token;
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
