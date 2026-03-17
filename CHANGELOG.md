# Changelog

All notable changes to this project will be documented in this file.

## [1.1.0] — 2026-03-17

### Added
- **VOD support** — browse and play movies and series from your Xtream Codes provider
  - Full movies list with metadata (poster, plot, cast, director, rating)
  - Series listing with per-episode resolution
  - Auto-detects container format (`mp4`, `mkv`, etc.) from API
- **Catchup / TV Archive** — replay past broadcasts using the timeshift URL format
  - Generates `timeshift/{user}/{pass}/{duration}/{start}/{id}.ts` URLs
  - Configurable per-provider (requires provider support)
- **REST API endpoint** — `GET /XtreamCodes/TestConnection`
  - Tests authentication and returns account status, expiry, max connections
  - Returns pre-built M3U and XMLTV URLs for easy copy-paste
- **Content summary endpoint** — `GET /XtreamCodes/ContentSummary`
  - Returns live channel count, VOD movie count, and series count
- **Improved config UI**
  - Tab-based mode switching (API credentials vs Direct M3U)
  - ⚡ Test Connection button with live result display
  - Auto-generated URL preview in settings page
  - Better styling and feedback states
- **manifest.json** for Jellyfin plugin catalogue registration
- **GitHub Actions** CI/CD — auto-build on push, auto-release on tag push

### Changed
- `ServiceRegistrator` now registers `XtreamVodProvider` as a singleton
- `PluginConfiguration` gained `EnableCatchup` property
- Stream URL builder is now static and reusable across services

---

## [1.0.0] — 2026-03-01

### Added
- Initial release
- Live TV channels via Xtream Codes API (`get_live_streams`)
- Channel categories and filtering
- EPG via `get_short_epg`
- Direct M3U playlist URL support (alternative to API credentials)
- XMLTV URL auto-generation
- Stream format selection (ts / m3u8 / rtmp)
- Configurable channel list refresh interval
- Embedded Jellyfin settings page (`configPage.html`)
