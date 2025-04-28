using AuthNetCore.DAL.IDAL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthNetCore.DAL
{
    public class AuthorizationService : IAuthorizationService
    {
        public async Task AccessLack()
        {
            await SystemAccessLack();
        }

        private async Task SystemAccessLack()
        {

        }
    }
}
