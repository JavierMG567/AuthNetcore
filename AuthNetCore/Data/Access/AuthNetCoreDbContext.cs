using AuthNetCore.Data.Models.DTos;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthNetCore.Data.Access
{
    public class AuthNetCoreDbContext : DbContext
    {
        public DbSet<AccountDto> Account { get; set; }
        public DbSet<AccountSessionsDto> AccountSessions { get; set; }
        public DbSet<BlackListTokenDto> BlackListToken { get; set; }
        public DbSet<AccountAuth> AccountAuth { get; set; }
        public AuthNetCoreDbContext(DbContextOptions<AuthNetCoreDbContext> options) : base(options) { }
    }
}
