using AuthNetCore.Data.Models.EntityFrameworkModels;
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
        public DbSet<Account> Accounts { get; set; }
        public DbSet<AccountSession> AccountSessions { get; set; }
        public DbSet<BlackListToken> BlackListToken { get; set; }
        public DbSet<AccountAuth> AccountAuth { get; set; }
        public AuthNetCoreDbContext(DbContextOptions<AuthNetCoreDbContext> options) : base(options) { }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
        }
    }
}
