using FluentValidation;
using FFM.Notification.Application.Commands;

namespace FFM.Notification.Application.Validators;

public class RegisterDeviceCommandValidator : AbstractValidator<RegisterDeviceCommand>
{
    private static readonly string[] AllowedPlatforms = { "ANDROID", "IOS", "WEB" };

    public RegisterDeviceCommandValidator()
    {
        RuleFor(x => x.EmployeeId)
            .GreaterThan(0)
            .WithMessage("EmployeeId must be a valid positive identifier.");

        RuleFor(x => x.DeviceId)
            .NotEmpty()
            .WithMessage("DeviceId is required and cannot be empty.")
            .MaximumLength(100)
            .WithMessage("DeviceId cannot exceed 100 characters.");

        RuleFor(x => x.Platform)
            .NotEmpty()
            .WithMessage("Platform is required.")
            .Must(p => AllowedPlatforms.Contains(p?.Trim().ToUpperInvariant()))
            .WithMessage("Unsupported device platform. Supported values are: ANDROID, IOS, WEB.");

        RuleFor(x => x.FcmToken)
            .NotEmpty()
            .WithMessage("FcmToken is required.")
            .MinimumLength(50)
            .WithMessage("FcmToken must be at least 50 characters.")
            .MaximumLength(500)
            .WithMessage("FcmToken cannot exceed 500 characters.");

        RuleFor(x => x.DeviceModel)
            .NotEmpty()
            .WithMessage("DeviceModel is required.")
            .MaximumLength(100);

        RuleFor(x => x.AppVersion)
            .NotEmpty()
            .WithMessage("AppVersion is required.")
            .MaximumLength(30);

        RuleFor(x => x.OsVersion)
            .NotEmpty()
            .WithMessage("OsVersion is required.")
            .MaximumLength(50);
    }
}

public class DeregisterDeviceCommandValidator : AbstractValidator<DeregisterDeviceCommand>
{
    public DeregisterDeviceCommandValidator()
    {
        RuleFor(x => x.EmployeeId)
            .GreaterThan(0)
            .WithMessage("EmployeeId must be a valid positive identifier.");

        RuleFor(x => x.DeviceId)
            .NotEmpty()
            .WithMessage("DeviceId is required and cannot be empty.");
    }
}

