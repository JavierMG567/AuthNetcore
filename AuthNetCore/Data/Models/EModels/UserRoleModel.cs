using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthNetCore.Data.Models.EModels
{
    public class UserRoleModel
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Role { get; set; }
    }
}
