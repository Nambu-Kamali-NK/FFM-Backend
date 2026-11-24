using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using FFM.Workforce.Application.Commands;
using FFM.Workforce.Application.DTOs;
using FFM.Workforce.Application.Interfaces;
using FFM.Workforce.Application.Queries;

namespace FFM.Workforce.Api.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize]
    public class EmployeesController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ICurrentUserService _currentUserService;

        public EmployeesController(IMediator mediator, ICurrentUserService currentUserService)
        {
            _mediator = mediator;
            _currentUserService = currentUserService;
        }

        /// <summary>
        /// Creates a new employee.
        /// </summary>
        [Authorize(Policy = "CanCreateEmployee")]
        [HttpPost]
        public async Task<IActionResult> Create(
            [FromBody] FFM.Workforce.Application.DTOs.CreateEmployeeRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "Request body cannot be empty.", "Validation error"));
            }

            var command = new CreateEmployeeCommand
            {
                EmployeeCode = request.EmployeeCode,
                Name = request.Name,
                Password = request.Password,
                RoleId = request.RoleId,
                ManagerId = request.ManagerId,
                Email = request.Email,
                Mobile = request.Mobile,
                ActorUserId = _currentUserService.GetUserId()
            };

            var employeeId = await _mediator.Send(command, cancellationToken);

            return StatusCode(
                StatusCodes.Status201Created,
                ApiResponse<object>.Ok(new { Id = employeeId }, "Employee created successfully"));
        }

        /// <summary>
        /// Gets employees accessible to the caller with pagination, date range filtering, and sorting.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(
            [FromQuery] GetAllEmployeesQuery? query,
            CancellationToken cancellationToken)
        {
            query ??= new GetAllEmployeesQuery();
            var pagedResult = await _mediator.Send(query, cancellationToken);
            return Ok(ApiResponse<PagedResult<EmployeeDto>>.Ok(pagedResult, "Employees retrieved successfully"));
        }

        /// <summary>
        /// Gets an employee by Workforce Employee ID.
        /// </summary>
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(
            int id,
            CancellationToken cancellationToken)
        {
            var employee = await _mediator.Send(
                new GetEmployeeByIdQuery(id),
                cancellationToken);

            if (employee == null)
                return NotFound(ApiResponse<object>.Fail("NOT_FOUND", $"Employee with ID {id} not found", "Resource not found"));

            return Ok(ApiResponse<EmployeeDto>.Ok(employee, "Employee retrieved successfully"));
        }

        /// <summary>
        /// Updates an existing employee's editable profile and workforce information.
        /// Target Employee ID is specified in the request body.
        /// </summary>
        [Authorize(Policy = "CanEditEmployee")]
        [HttpPut]
        public async Task<IActionResult> Edit(
            [FromBody] FFM.Workforce.Application.DTOs.EditEmployeeRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "Request body cannot be empty.", "Validation error"));
            }

            if (request.Id <= 0)
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "Invalid employee ID.", "Validation error"));
            }

            var command = new EditEmployeeCommand
            {
                Id = request.Id,
                Name = request.Name,
                Email = request.Email,
                Mobile = request.Mobile,
                RoleId = request.RoleId,
                ManagerId = request.ManagerId,
                ActorUserId = _currentUserService.GetUserId()
            };

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(ApiResponse<EditEmployeeResponseDto>.Ok(result, "Employee updated successfully"));
        }

        /// <summary>
        /// Updates a single employee's status (Activates if IsActive=true, Deactivates if IsActive=false).
        /// </summary>
        [Authorize(Policy = "CanEditEmployee")]
        [HttpPost("status")]
        [Consumes("application/json")]
        [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> UpdateSingleEmployeeStatus(
            [FromBody] FFM.Workforce.Application.DTOs.SingleEmployeeStatusChangeRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "Request body cannot be empty.", "Validation error"));
            }

            if (request.Id <= 0)
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "Invalid employee ID.", "Validation error"));
            }

            if (request.IsActive)
            {
                var command = new ActivateEmployeeCommand
                {
                    EmployeeId = request.Id,
                    ActorUserId = _currentUserService.GetUserId()
                };
                await _mediator.Send(command, cancellationToken);
                return Ok(ApiResponse<object>.Ok(new { id = request.Id, isActive = true }, "Employee activated successfully."));
            }
            else
            {
                var command = new DeactivateEmployeeCommand
                {
                    EmployeeId = request.Id,
                    ActorUserId = _currentUserService.GetUserId()
                };
                await _mediator.Send(command, cancellationToken);
                return Ok(ApiResponse<object>.Ok(new { id = request.Id, isActive = false }, "Employee deactivated successfully."));
            }
        }

        /// <summary>
        /// Endpoint 2: Multiple Employee Batch Status Endpoint (Activates if IsActive=true, Deactivates if IsActive=false).
        /// </summary>
        [Authorize(Policy = "CanEditEmployee")]
        [HttpPost("status-batch")]
        public async Task<IActionResult> UpdateBatchEmployeeStatus(
            [FromBody] FFM.Workforce.Application.DTOs.BatchEmployeeStatusChangeRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "Request body cannot be empty.", "Validation error"));
            }

            if (request.EmployeeIds == null || !request.EmployeeIds.Any())
            {
                return BadRequest(ApiResponse<object>.Fail("BAD_REQUEST", "At least one employee ID must be provided.", "Validation error"));
            }

            if (request.IsActive)
            {
                var command = new BatchActivateEmployeesCommand
                {
                    EmployeeIds = request.EmployeeIds,
                    ActorUserId = _currentUserService.GetUserId()
                };
                var count = await _mediator.Send(command, cancellationToken);
                return Ok(ApiResponse<object>.Ok(new { count, isActive = true }, $"{count} employee(s) activated successfully."));
            }
            else
            {
                var command = new BatchDeactivateEmployeesCommand
                {
                    EmployeeIds = request.EmployeeIds,
                    ActorUserId = _currentUserService.GetUserId()
                };
                var count = await _mediator.Send(command, cancellationToken);
                return Ok(ApiResponse<object>.Ok(new { count, isActive = false }, $"{count} employee(s) deactivated successfully."));
            }
        }
    }
}
