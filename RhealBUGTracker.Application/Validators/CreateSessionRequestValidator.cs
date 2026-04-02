using FluentValidation;
using RhealBUGTracker.Application.DTOs;

namespace RhealBUGTracker.Application.Validators;

public class CreateSessionRequestValidator : AbstractValidator<CreateSessionRequest>
{
    public CreateSessionRequestValidator()
    {
        When(x => !string.IsNullOrEmpty(x.RepoUrl), () =>
        {
            RuleFor(x => x.RepoUrl)
                .Must(url => Uri.TryCreate(url, UriKind.Absolute, out var u) &&
                             (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp))
                .WithMessage("RepoUrl must be a valid URL.");
        });
    }
}
