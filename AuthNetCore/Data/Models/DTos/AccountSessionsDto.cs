using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthNetCore.Data.Models.DTos
{
    public class AccountSessionsDto
    {
        [Key]
        public int Id { get; set; }
        public int AccountId { get; set; }
        public string Token { get; set; }
        public bool IsRevoked { get; set; }
    }
}
