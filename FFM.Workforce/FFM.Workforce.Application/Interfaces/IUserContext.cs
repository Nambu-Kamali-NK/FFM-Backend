using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FFM.Workforce.Application.Interfaces
{
    /// <summary>
    /// Provides information about the currently authenticated user extracted from the JWT.
    /// </summary>
    public interface ICurrentUserService
    {
        int GetUserId();
        IEnumerable<string> GetRoles();
    }
}



