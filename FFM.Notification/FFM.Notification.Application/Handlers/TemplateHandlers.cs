using FFM.Notification.Application.Commands;
using FFM.Notification.Application.DTOs;
using FFM.Notification.Application.Queries;
using FFM.Notification.Application.Services;
using FFM.Notification.Domain.Entities;
using FFM.Notification.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FFM.Notification.Application.Handlers;

public class CreateNotificationTemplateCommandHandler : IRequestHandler<CreateNotificationTemplateCommand, NotificationTemplateResponse>
{
    private readonly INotificationTemplateRepository _repository;
    private readonly ILogger<CreateNotificationTemplateCommandHandler> _logger;

    public CreateNotificationTemplateCommandHandler(
        INotificationTemplateRepository repository,
        ILogger<CreateNotificationTemplateCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<NotificationTemplateResponse> Handle(CreateNotificationTemplateCommand request, CancellationToken cancellationToken)
    {
        var normalizedCode = request.TemplateCode.Trim().ToUpperInvariant();
        var normalizedChannel = request.Channel.Trim().ToUpperInvariant();

        var existing = await _repository.GetByCodeAndChannelAsync(normalizedCode, normalizedChannel, cancellationToken);
        if (existing != null)
        {
            throw new InvalidOperationException($"A template with code '{normalizedCode}' and channel '{normalizedChannel}' already exists.");
        }

        _logger.LogInformation("Creating new NotificationTemplate [{Code}] for channel [{Channel}]", normalizedCode, normalizedChannel);

        var template = new NotificationTemplate(
            normalizedCode,
            normalizedChannel,
            request.Title,
            request.BodyTemplate,
            request.Description,
            request.CreatedByUserId
        );

        await _repository.AddAsync(template, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return MapToResponse(template);
    }

    private static NotificationTemplateResponse MapToResponse(NotificationTemplate t) => new()
    {
        TemplateId = t.Id,
        TemplateCode = t.TemplateCode,
        Channel = t.Channel,
        Title = t.Title,
        BodyTemplate = t.BodyTemplate,
        Description = t.Description,
        IsActive = t.IsActive,
        CreatedByUserId = t.CreatedByUserId,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt
    };
}

public class UpdateNotificationTemplateCommandHandler : IRequestHandler<UpdateNotificationTemplateCommand, NotificationTemplateResponse>
{
    private readonly INotificationTemplateRepository _repository;
    private readonly ILogger<UpdateNotificationTemplateCommandHandler> _logger;

    public UpdateNotificationTemplateCommandHandler(
        INotificationTemplateRepository repository,
        ILogger<UpdateNotificationTemplateCommandHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<NotificationTemplateResponse> Handle(UpdateNotificationTemplateCommand request, CancellationToken cancellationToken)
    {
        var template = await _repository.GetByIdAsync(request.TemplateId, cancellationToken);
        if (template == null)
        {
            throw new KeyNotFoundException($"Notification template with ID {request.TemplateId} was not found.");
        }

        _logger.LogInformation("Updating NotificationTemplate ID {Id}", request.TemplateId);

        template.Update(request.Title, request.BodyTemplate, request.Description, request.IsActive, request.UpdatedByUserId);
        await _repository.UpdateAsync(template, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        return new NotificationTemplateResponse
        {
            TemplateId = template.Id,
            TemplateCode = template.TemplateCode,
            Channel = template.Channel,
            Title = template.Title,
            BodyTemplate = template.BodyTemplate,
            Description = template.Description,
            IsActive = template.IsActive,
            CreatedByUserId = template.CreatedByUserId,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        };
    }
}

public class RenderTemplateQueryHandler : IRequestHandler<RenderTemplateQuery, RenderedTemplateResponse?>
{
    private readonly INotificationTemplateRepository _repository;
    private readonly ITemplateInterpolationEngine _interpolationEngine;

    public RenderTemplateQueryHandler(
        INotificationTemplateRepository repository,
        ITemplateInterpolationEngine interpolationEngine)
    {
        _repository = repository;
        _interpolationEngine = interpolationEngine;
    }

    public async Task<RenderedTemplateResponse?> Handle(RenderTemplateQuery request, CancellationToken cancellationToken)
    {
        var normalizedCode = request.TemplateCode.Trim().ToUpperInvariant();
        var normalizedChannel = request.Channel.Trim().ToUpperInvariant();

        var template = await _repository.GetByCodeAndChannelAsync(normalizedCode, normalizedChannel, cancellationToken);
        if (template == null || !template.IsActive)
        {
            return null;
        }

        var renderedTitle = template.Title != null
            ? _interpolationEngine.Interpolate(template.Title, request.Placeholders)
            : null;

        var renderedBody = _interpolationEngine.Interpolate(template.BodyTemplate, request.Placeholders);

        return new RenderedTemplateResponse
        {
            TemplateCode = template.TemplateCode,
            Channel = template.Channel,
            RenderedTitle = renderedTitle,
            RenderedBody = renderedBody
        };
    }
}

public class GetTemplateByCodeQueryHandler : IRequestHandler<GetTemplateByCodeQuery, NotificationTemplateResponse?>
{
    private readonly INotificationTemplateRepository _repository;

    public GetTemplateByCodeQueryHandler(INotificationTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<NotificationTemplateResponse?> Handle(GetTemplateByCodeQuery request, CancellationToken cancellationToken)
    {
        var template = await _repository.GetByCodeAndChannelAsync(
            request.Code.Trim().ToUpperInvariant(),
            request.Channel.Trim().ToUpperInvariant(),
            cancellationToken);

        if (template == null) return null;

        return new NotificationTemplateResponse
        {
            TemplateId = template.Id,
            TemplateCode = template.TemplateCode,
            Channel = template.Channel,
            Title = template.Title,
            BodyTemplate = template.BodyTemplate,
            Description = template.Description,
            IsActive = template.IsActive,
            CreatedByUserId = template.CreatedByUserId,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt
        };
    }
}

public class ListNotificationTemplatesQueryHandler : IRequestHandler<ListNotificationTemplatesQuery, List<NotificationTemplateResponse>>
{
    private readonly INotificationTemplateRepository _repository;

    public ListNotificationTemplatesQueryHandler(INotificationTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<NotificationTemplateResponse>> Handle(ListNotificationTemplatesQuery request, CancellationToken cancellationToken)
    {
        var templates = await _repository.GetAllAsync(request.Channel, request.OnlyActive, cancellationToken);
        return templates.Select(t => new NotificationTemplateResponse
        {
            TemplateId = t.Id,
            TemplateCode = t.TemplateCode,
            Channel = t.Channel,
            Title = t.Title,
            BodyTemplate = t.BodyTemplate,
            Description = t.Description,
            IsActive = t.IsActive,
            CreatedByUserId = t.CreatedByUserId,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        }).ToList();
    }
}

public class SearchNotificationTemplatesQueryHandler : IRequestHandler<SearchNotificationTemplatesQuery, List<NotificationTemplateResponse>>
{
    private readonly INotificationTemplateRepository _repository;

    public SearchNotificationTemplatesQueryHandler(INotificationTemplateRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<NotificationTemplateResponse>> Handle(SearchNotificationTemplatesQuery request, CancellationToken cancellationToken)
    {
        var templates = await _repository.SearchAsync(request.Query, cancellationToken);
        return templates.Select(t => new NotificationTemplateResponse
        {
            TemplateId = t.Id,
            TemplateCode = t.TemplateCode,
            Channel = t.Channel,
            Title = t.Title,
            BodyTemplate = t.BodyTemplate,
            Description = t.Description,
            IsActive = t.IsActive,
            CreatedByUserId = t.CreatedByUserId,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt
        }).ToList();
    }
}

