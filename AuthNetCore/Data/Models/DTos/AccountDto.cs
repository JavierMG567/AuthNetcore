using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthNetCore.Data.Models.DTOs
{
    public class AccountDto
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public DateOnly BirthDate { get; set; }
        public bool IsLocked { get; set; }
    }
}
