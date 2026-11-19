using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PhoneStore.BuildingBlocks.Application;
using PhoneStore.BuildingBlocks.Common;
using PhoneStore.Modules.Identity.Application.DTOs;
using PhoneStore.Modules.Identity.Application.Services;

namespace PhoneStore.Modules.Identity.Endpoints;

[ApiController]
[Route("api/v1/admin/audit")]
[Authorize(Policy = AuthorizationPolicies.RequireAdmin)]
public sealed class IdentityAuditController(IdentityService identity) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] AuditFilterQuery query, CancellationToken ct) =>
        Ok(ApiResponse<PagedResult<AuditDto>>.Ok(await identity.GetAuditAsync(query, ct)));
}
