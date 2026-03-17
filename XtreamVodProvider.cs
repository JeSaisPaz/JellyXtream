using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.XtreamCodes.Api;
using Jellyfin.Plugin.XtreamCodes.Models;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.MediaInfo;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.XtreamCodes.Vod;

/// <summary>
/// Provides VOD movie and series metadata + stream resolution from Xtream Codes.
/// </summary>
public class XtreamVodProvider
{
    private readonly ILogger<XtreamVodProvider> _logger;
    private readonly XtreamCodesApiClient _apiClient;

    // Simple in-memory cache for the session
    private List<XtreamVodStream>?   _vodCache;
    private List<XtreamSeries>?      _seriesCache;
    private List<XtreamCategory>?    _vodCategoryCache;
    private List<XtreamCategory>?    _seriesCategoryCache;
    private DateTime _lastVodRefresh    = DateTime.MinValue;
    private DateTime _lastSeriesRefresh = DateTime.MinValue;

    public XtreamVodProvider(ILogger<XtreamVodProvider> logger, IHttpClientFactory httpClientFactory)
    {
        _logger    = logger;
        _apiClient = new XtreamCodesApiClient(
            httpClientFactory.CreateClient(nameof(XtreamVodProvider)),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<XtreamCodesApiClient>.Instance);
    }

    // ── Config helper ─────────────────────────────────────────────────────────

    private static (string s, string u, string p, Configuration.PluginConfiguration cfg) GetConfig()
    {
        var cfg = Plugin.Instance?.Configuration
            ?? throw new InvalidOperationException("Plugin not configured.");
        return (cfg.ServerUrl, cfg.Username, cfg.Password, cfg);
    }

    // ── VOD Movies ────────────────────────────────────────────────────────────

    /// <summary>Returns all VOD movies, optionally filtered by category.</summary>
    public async Task<IEnumerable<XtreamVodStream>> GetMoviesAsync(CancellationToken ct = default)
    {
        var (s, u, p, cfg) = GetConfig();
        var ttl = TimeSpan.FromHours(cfg.RefreshIntervalHours);

        if (_vodCache is null || DateTime.UtcNow - _lastVodRefresh > ttl)
        {
            _vodCategoryCache = await _apiClient.GetVodCategoriesAsync(s, u, p, ct).ConfigureAwait(false);
            _vodCache         = await _apiClient.GetVodStreamsAsync(s, u, p, null, ct).ConfigureAwait(false);
            _lastVodRefresh   = DateTime.UtcNow;
            _logger.LogInformation("Loaded {Count} VOD streams.", _vodCache.Count);
        }

        return _vodCache;
    }

    /// <summary>Resolves a playback URL for a VOD movie.</summary>
    public async Task<MediaSourceInfo> GetMovieStreamAsync(int vodId, CancellationToken ct = default)
    {
        var (s, u, p, cfg) = GetConfig();
        var info = await _apiClient.GetVodInfoAsync(s, u, p, vodId, ct).ConfigureAwait(false);
        var ext  = info?.MovieData?.ContainerExtension ?? "mp4";
        var url  = XtreamCodesApiClient.BuildVodUrl(s, u, p, vodId, ext);

        return new MediaSourceInfo
        {
            Id                   = vodId.ToString(CultureInfo.InvariantCulture),
            Path                 = url,
            Protocol             = MediaProtocol.Http,
            IsRemote             = true,
            SupportsDirectPlay   = true,
            SupportsDirectStream = true,
            IsInfiniteStream     = false
        };
    }

    // ── Series ────────────────────────────────────────────────────────────────

    /// <summary>Returns all series listings.</summary>
    public async Task<IEnumerable<XtreamSeries>> GetSeriesAsync(CancellationToken ct = default)
    {
        var (s, u, p, cfg) = GetConfig();
        var ttl = TimeSpan.FromHours(cfg.RefreshIntervalHours);

        if (_seriesCache is null || DateTime.UtcNow - _lastSeriesRefresh > ttl)
        {
            _seriesCategoryCache = await _apiClient.GetSeriesCategoriesAsync(s, u, p, ct).ConfigureAwait(false);
            _seriesCache         = await _apiClient.GetSeriesAsync(s, u, p, null, ct).ConfigureAwait(false);
            _lastSeriesRefresh   = DateTime.UtcNow;
            _logger.LogInformation("Loaded {Count} series.", _seriesCache.Count);
        }

        return _seriesCache;
    }

    /// <summary>Returns all episodes for a given series.</summary>
    public async Task<IEnumerable<XtreamEpisode>> GetEpisodesAsync(int seriesId, CancellationToken ct = default)
    {
        var (s, u, p, _) = GetConfig();
        var info = await _apiClient.GetSeriesInfoAsync(s, u, p, seriesId, ct).ConfigureAwait(false);
        if (info?.Episodes is null) return Enumerable.Empty<XtreamEpisode>();

        return info.Episodes.Values.SelectMany(e => e);
    }

    /// <summary>Resolves a playback URL for a specific episode.</summary>
    public Task<MediaSourceInfo> GetEpisodeStreamAsync(string episodeId, string ext, CancellationToken ct = default)
    {
        var (s, u, p, _) = GetConfig();
        var url = XtreamCodesApiClient.BuildEpisodeUrl(s, u, p, episodeId, ext);

        return Task.FromResult(new MediaSourceInfo
        {
            Id                   = episodeId,
            Path                 = url,
            Protocol             = MediaProtocol.Http,
            IsRemote             = true,
            SupportsDirectPlay   = true,
            SupportsDirectStream = true,
            IsInfiniteStream     = false
        });
    }

    // ── Category helpers ──────────────────────────────────────────────────────

    /// <summary>Returns all VOD categories.</summary>
    public async Task<IEnumerable<XtreamCategory>> GetVodCategoriesAsync(CancellationToken ct = default)
    {
        await GetMoviesAsync(ct).ConfigureAwait(false); // ensures cache is populated
        return _vodCategoryCache ?? Enumerable.Empty<XtreamCategory>();
    }

    /// <summary>Returns all series categories.</summary>
    public async Task<IEnumerable<XtreamCategory>> GetSeriesCategoriesAsync(CancellationToken ct = default)
    {
        await GetSeriesAsync(ct).ConfigureAwait(false);
        return _seriesCategoryCache ?? Enumerable.Empty<XtreamCategory>();
    }
}
