using System;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamCodes.Api;
using MediaBrowser.Controller.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.XtreamCodes.Controllers;

/// <summary>
/// REST API endpoints exposed by the Xtream Codes plugin.
/// Accessible at /XtreamCodes/* from the Jellyfin Dashboard.
/// </summary>
[ApiController]
[Route("XtreamCodes")]
[Authorize(Policy = "DefaultAuthorization")]
public class XtreamCodesController : ControllerBase
{
    private readonly ILogger<XtreamCodesController> _logger;
    private readonly System.Net.Http.IHttpClientFactory _httpClientFactory;

    public XtreamCodesController(
        ILogger<XtreamCodesController> logger,
        System.Net.Http.IHttpClientFactory httpClientFactory)
    {
        _logger            = logger;
        _httpClientFactory = httpClientFactory;
    }

    // ── Connection test ───────────────────────────────────────────────────────

    /// <summary>
    /// Tests connectivity and authentication to an Xtream Codes server.
    /// </summary>
    /// <param name="serverUrl">Full server URL (e.g. http://host:8080).</param>
    /// <param name="username">Xtream Codes username.</param>
    /// <param name="password">Xtream Codes password.</param>
    /// <returns>Connection test result with account status.</returns>
    [HttpGet("TestConnection")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<ConnectionTestResult>> TestConnectionAsync(
        [FromQuery] string serverUrl,
        [FromQuery] string username,
        [FromQuery] string password,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(serverUrl) ||
            string.IsNullOrWhiteSpace(username)  ||
            string.IsNullOrWhiteSpace(password))
        {
            return BadRequest("serverUrl, username and password are required.");
        }

        try
        {
            var client = new XtreamCodesApiClient(
                _httpClientFactory.CreateClient("XtreamTest"),
                Microsoft.Extensions.Logging.Abstractions.NullLogger<XtreamCodesApiClient>.Instance);

            var info = await client.AuthenticateAsync(serverUrl, username, password, cancellationToken)
                .ConfigureAwait(false);

            if (info?.UserInfo is null)
            {
                return StatusCode(StatusCodes.Status502BadGateway, new ConnectionTestResult
                {
                    Success = false,
                    Message = "Server responded but returned no user info. Check your credentials."
                });
            }

            var userInfo = info.UserInfo;
            var status   = userInfo.Status;

            if (!string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return Ok(new ConnectionTestResult
                {
                    Success        = false,
                    Message        = $"Account status is '{status}'. Active account required.",
                    AccountStatus  = status,
                    MaxConnections = userInfo.MaxConnections,
                    ActiveCons     = userInfo.ActiveCons,
                    ExpiryDate     = userInfo.ExpDate
                });
            }

            return Ok(new ConnectionTestResult
            {
                Success        = true,
                Message        = "Connection successful!",
                AccountStatus  = status,
                MaxConnections = userInfo.MaxConnections,
                ActiveCons     = userInfo.ActiveCons,
                ExpiryDate     = userInfo.ExpDate,
                ServerTimezone = info.ServerInfo?.Timezone,
                M3uUrl         = XtreamCodesApiClient.BuildM3uUrl(serverUrl, username, password),
                XmltvUrl       = XtreamCodesApiClient.BuildXmltvUrl(serverUrl, username, password)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test failed for {Server}", serverUrl);
            return StatusCode(StatusCodes.Status502BadGateway, new ConnectionTestResult
            {
                Success = false,
                Message = $"Could not reach server: {ex.Message}"
            });
        }
    }

    // ── Channel count ─────────────────────────────────────────────────────────

    /// <summary>Returns a quick count of live, VOD and series content from the provider.</summary>
    [HttpGet("ContentSummary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<ContentSummary>> GetContentSummaryAsync(CancellationToken cancellationToken)
    {
        var cfg = Plugin.Instance?.Configuration;
        if (cfg is null || string.IsNullOrEmpty(cfg.ServerUrl))
            return BadRequest("Plugin is not configured.");

        var client = new XtreamCodesApiClient(
            _httpClientFactory.CreateClient("XtreamSummary"),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<XtreamCodesApiClient>.Instance);

        var (s, u, p) = (cfg.ServerUrl, cfg.Username, cfg.Password);

        var liveTask   = client.GetLiveStreamsAsync(s, u, p, null, cancellationToken);
        var vodTask    = cfg.EnableVod ? client.GetVodStreamsAsync(s, u, p, null, cancellationToken) : Task.FromResult(new System.Collections.Generic.List<Models.XtreamVodStream>());
        var seriesTask = cfg.EnableVod ? client.GetSeriesAsync(s, u, p, null, cancellationToken) : Task.FromResult(new System.Collections.Generic.List<Models.XtreamSeries>());

        await Task.WhenAll(liveTask, vodTask, seriesTask).ConfigureAwait(false);

        return Ok(new ContentSummary
        {
            LiveChannels = liveTask.Result.Count,
            VodMovies    = vodTask.Result.Count,
            Series       = seriesTask.Result.Count
        });
    }
}

// ── Response DTOs ─────────────────────────────────────────────────────────────

public class ConnectionTestResult
{
    public bool    Success        { get; set; }
    public string  Message        { get; set; } = string.Empty;
    public string? AccountStatus  { get; set; }
    public string? MaxConnections { get; set; }
    public string? ActiveCons     { get; set; }
    public string? ExpiryDate     { get; set; }
    public string? ServerTimezone { get; set; }
    public string? M3uUrl         { get; set; }
    public string? XmltvUrl       { get; set; }
}

public class ContentSummary
{
    public int LiveChannels { get; set; }
    public int VodMovies    { get; set; }
    public int Series       { get; set; }
}
