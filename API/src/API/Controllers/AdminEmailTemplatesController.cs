using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Features.EmailTemplates;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>Browse, edit, preview, and toggle transactional/marketing email templates.</summary>
[Route("api/admin/email-templates")]
[RequireModule(Modules.EmailTemplates)]
public class AdminEmailTemplatesController : ApiControllerBase
{
    /// <summary>List templates, optionally filtered by category, enabled state, and a name/key search term.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EmailTemplateSummaryDto>>> List(
        [FromQuery] NotificationCategory? category, [FromQuery] bool? enabled, [FromQuery] string? search)
        => Ok(await Mediator.Send(new ListEmailTemplatesQuery(category, enabled, search)));

    /// <summary>Get a single template by key.</summary>
    [HttpGet("{key}")]
    public async Task<ActionResult<EmailTemplateDto>> Get(string key)
        => Ok(await Mediator.Send(new GetEmailTemplateQuery(key)));

    /// <summary>Render a template preview using sample token values.</summary>
    [HttpGet("{key}/preview")]
    public async Task<ActionResult<EmailTemplatePreviewDto>> Preview(string key)
        => Ok(await Mediator.Send(new PreviewEmailTemplateQuery(key)));

    /// <summary>Update a template's editable content.</summary>
    [HttpPut("{key}")]
    public async Task<ActionResult<EmailTemplateDto>> Update(string key, [FromBody] UpdateEmailTemplateRequest body)
        => Ok(await Mediator.Send(new UpdateEmailTemplateCommand(
            key, body.Name, body.SubjectLine, body.HtmlBody, body.PlainTextBody, body.Description)));

    /// <summary>Enable or disable a template.</summary>
    [HttpPut("{key}/enabled")]
    public async Task<ActionResult<EmailTemplateDto>> SetEnabled(string key, [FromBody] SetEnabledRequest body)
        => Ok(await Mediator.Send(new SetEmailTemplateEnabledCommand(key, body.Enabled, body.Confirm)));

    public record UpdateEmailTemplateRequest(
        string Name, string SubjectLine, string HtmlBody, string? PlainTextBody, string? Description);

    public record SetEnabledRequest(bool Enabled, bool Confirm = false);
}
