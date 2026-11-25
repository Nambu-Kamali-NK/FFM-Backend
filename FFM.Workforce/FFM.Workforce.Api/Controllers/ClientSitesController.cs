using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FFM.Workforce.Infrastructure;
using FFM.Workforce.Domain.Entities;

namespace FFM.Workforce.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ClientSitesController : ControllerBase
{
    private readonly WorkforceDbContext _dbContext;

    public ClientSitesController(WorkforceDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? searchTerm = null,
        [FromQuery] bool? isActive = true,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.ClientSites.AsNoTracking().AsQueryable();

        if (isActive.HasValue)
        {
            query = query.Where(s => s.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim().ToLower();
            query = query.Where(s =>
                s.Name.ToLower().Contains(term) ||
                s.SiteCode.ToLower().Contains(term) ||
                (s.Address != null && s.Address.ToLower().Contains(term)));
        }

        var items = await query
            .OrderBy(s => s.Name)
            .Select(s => new
            {
                id = s.Id,
                siteCode = s.SiteCode,
                name = s.Name,
                address = s.Address ?? "Muscat Municipal Sector",
                latitude = s.Latitude,
                longitude = s.Longitude,
                radiusMeters = s.RadiusMeters ?? 150,
                isActive = s.IsActive
            })
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            success = true,
            message = "Client sites retrieved successfully.",
            data = items
        });
    }
}
