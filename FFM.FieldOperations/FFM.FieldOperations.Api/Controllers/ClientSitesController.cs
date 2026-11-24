using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFM.FieldOperations.Application.Commands;
using FFM.FieldOperations.Application.DTOs;
using FFM.FieldOperations.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FFM.FieldOperations.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class ClientSitesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IClientSiteRepository _repository;
        private readonly ICurrentUserService _currentUserService;

        public ClientSitesController(
            IMediator mediator,
            IClientSiteRepository repository,
            ICurrentUserService currentUserService)
        {
            _mediator = mediator;
            _repository = repository;
            _currentUserService = currentUserService;
        }

        /// <summary>
        /// Registers a new customer or client site with geographic coordinates and geofence radius.
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ApiResponse<ClientSiteDto>), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> RegisterClientSite(
            [FromBody] CreateClientSiteRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "Request body cannot be empty.", "Validation error"));
            }

            var command = new CreateClientSiteCommand
            {
                ActorUserId = _currentUserService.GetUserId(),
                SiteCode = request.SiteCode,
                Name = request.Name,
                ContactPerson = request.ContactPerson,
                ContactPhone = request.ContactPhone,
                ContactEmail = request.ContactEmail,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                RadiusMeters = request.RadiusMeters,
                Address = request.Address,
                City = request.City,
                State = request.State,
                PostalCode = request.PostalCode
            };

            try
            {
                var result = await _mediator.Send(command, cancellationToken);
                return StatusCode(StatusCodes.Status201Created, ApiResponse<ClientSiteDto>.Ok(result, "Client site registered successfully."));
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(ApiResponse<object>.Fail("DUPLICATE_SITE_CODE", ex.Message));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse<object>.Fail("VALIDATION_ERROR", ex.Message));
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail("FORBIDDEN", ex.Message));
            }
        }

        /// <summary>
        /// Retrieves all active client sites.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(ApiResponse<List<ClientSiteDto>>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAllClientSites(
            [FromQuery] bool activeOnly = true,
            CancellationToken cancellationToken = default)
        {
            var sites = await _repository.GetAllAsync(activeOnly);
            var dtos = sites.Select(s => new ClientSiteDto
            {
                Id = s.Id,
                SiteCode = s.SiteCode,
                Name = s.Name,
                ContactPerson = s.ContactPerson,
                ContactPhone = s.ContactPhone,
                ContactEmail = s.ContactEmail,
                Latitude = s.Latitude,
                Longitude = s.Longitude,
                RadiusMeters = s.RadiusMeters,
                Address = s.Address,
                City = s.City,
                State = s.State,
                PostalCode = s.PostalCode,
                IsActive = s.IsActive,
                CreatedAt = s.CreatedAt
            }).ToList();

            return Ok(ApiResponse<List<ClientSiteDto>>.Ok(dtos, "Client sites retrieved successfully"));
        }

        /// <summary>
        /// Retrieves detailed information for a specific client site.
        /// </summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ApiResponse<ClientSiteDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetClientSiteById(int id)
        {
            var site = await _repository.GetByIdAsync(id);
            if (site == null)
            {
                return NotFound(ApiResponse<object>.Fail("NOT_FOUND", $"Client site with ID {id} not found."));
            }

            var dto = new ClientSiteDto
            {
                Id = site.Id,
                SiteCode = site.SiteCode,
                Name = site.Name,
                ContactPerson = site.ContactPerson,
                ContactPhone = site.ContactPhone,
                ContactEmail = site.ContactEmail,
                Latitude = site.Latitude,
                Longitude = site.Longitude,
                RadiusMeters = site.RadiusMeters,
                Address = site.Address,
                City = site.City,
                State = site.State,
                PostalCode = site.PostalCode,
                IsActive = site.IsActive,
                CreatedAt = site.CreatedAt
            };

            return Ok(ApiResponse<ClientSiteDto>.Ok(dto, "Client site retrieved successfully"));
        }
    }
}
