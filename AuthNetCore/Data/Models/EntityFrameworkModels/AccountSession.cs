using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthNetCore.Data.Models.EntityFrameworkModels
{
    public class AccountSession
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public int AccountId { get; set; }
        public string Token { get; set; }
        public bool IsRevoked { get; set; }
        public DateTime Created { get; set; }
    }
}
