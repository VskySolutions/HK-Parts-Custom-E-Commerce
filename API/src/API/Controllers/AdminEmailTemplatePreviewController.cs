using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using HKPartsEcommerce.Application.Common.Authorization;
using HKPartsEcommerce.API.Authorization;
using HKPartsEcommerce.Application.Features.EmailTemplates;

namespace HKPartsEcommerce.API.Controllers;

/// <summary>
/// Full-fidelity email-template preview and test-send (WO-79, REQ-ENT-003 / REQ-ENT-004). Kept as a
/// separate controller from <see cref="AdminEmailTemplatesController"/> so that controller is untouched;
/// its actions use distinct verbs/paths, so there is no route clash with the existing GET preview.
/// </summary>
[Route("api/admin/email-templates")]
[RequireModule(Modules.EmailTemplates)]
public class AdminEmailTemplatePreviewController : ApiControllerBase
{
    /// <summary>
    /// Render a template with sample data, returning the subject, from-name, and both the HTML and
    /// plain-text bodies (AC-ENT-003.2/.3/.4). Uses POST (not GET) so an optional body can carry unsaved
    /// edits to preview without saving first (AC-ENT-003.1); omit the body to preview the stored template.
    /// </summary>
    [HttpPost("{key}/preview")]
    public async Task<ActionResult<EmailTemplatePreviewResult>> Preview(
        string key,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PreviewEmailTemplateRequest? body)
        => Ok(await Mediator.Send(new PreviewEmailTemplateContentQuery(
            key, body?.Subject, body?.HtmlBody, body?.PlainTextBody)));

    /// <summary>
    /// Render with sample data and enqueue a test email to the given recipient, routed via the SMTP
    /// account assigned to the template's category (REQ-ENT-004). Returns a dispatch confirmation or the
    /// failure reason.
    /// </summary>
    [HttpPost("{key}/test-send")]
    public async Task<ActionResult<EmailTemplateTestSendResult>> TestSend(
        string key, [FromBody] TestSendEmailTemplateRequest body)
        => Ok(await Mediator.Send(new TestSendEmailTemplateCommand(key, body.RecipientEmail)));

    /// <summary>Optional unsaved content to preview; any omitted field falls back to the stored template.</summary>
    public record PreviewEmailTemplateRequest(string? Subject, string? HtmlBody, string? PlainTextBody);

    public record TestSendEmailTemplateRequest(string RecipientEmail);
}
