using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AuthNetCore.DAL.IDAL
{
    internal interface IAuthorizationService
    {
        Task AccessLack();
    }
}
