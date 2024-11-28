using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace DomainBlockList;

internal class DomainBlockCompiler
{
    private const string _zoneFormat = "zone \"{0}\" {{ type primary; file \"/etc/bind/zones/db.blocks\"; }};";
    private const string _hostsFileFormat = "127.0.0.1 {0}";

    private readonly HttpClient _client;
    private readonly ILogger _logger;
    private readonly List<string> _sources;
    private readonly List<string> _localBlockList;

    public DomainBlockCompiler(ILogger logger, List<string> sources, List<string> localBlockList)
    {
        _client = new HttpClient();
        _logger = logger;
        _sources = sources;
        _localBlockList = localBlockList;
    }

    public async Task GenerateZonesAsync(FileInfo fileName, FormatType formatType, string format)
    {
        _logger.LogInformation("Compiling block list in {fileName} with {formatType} format", fileName, formatType);
        var domains = await DownloadDomainsAsync().ConfigureAwait(false);
        domains.AddRange(_localBlockList);
        domains = [.. domains.Distinct().Order()];
        _logger.LogInformation("Added {localCount} items from the local block list.", _localBlockList.Count);
        GenerateZonesFile(fileName, domains, GetFormat(formatType, format));
        _logger.LogInformation("File successfully generated {fileName}", fileName);
    }

    private async Task<List<string>> DownloadDomainsAsync()
    {
        var result = new List<string>();

        foreach (var source in _sources)
        {
            _logger.LogInformation("Downloading {source}", source);
            var records = string.Empty;

            try
            {
                var recordsResponse = await _client.GetAsync(source);

                if (!recordsResponse.IsSuccessStatusCode)
                {
                    _logger.LogError($"Failed to download {source}");
                    continue;
                }

                records = await recordsResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while downloading {source}", source);
            }

            var domains = records.Split('\n').Where(x => !x.StartsWith("#")).ToList();
            _logger.LogInformation("Completed downloading {source}. Total lines: {count}", source, domains.Count);

            foreach (var zone in domains)
            {
                var comment = zone.IndexOf('#');
                var domainItem = comment > 0 ? zone[..comment] : zone;
                domainItem = domainItem.Replace("127.0.0.1", "");
                domainItem = domainItem.Replace("localhost", "");
                domainItem = domainItem.Trim();

                if (!domainItem.Contains('.'))
                {
                    continue;
                }

                if (domainItem.StartsWith("www."))
                {
                    domainItem = domainItem[4..];
                }

                if (Uri.CheckHostName(domainItem) != UriHostNameType.Unknown)
                {
                    result.Add(domainItem);
                }
            }
        }

        result = result.Distinct().ToList();
        _logger.LogInformation("Completed download from all sources. Total unique domains: {count}", result.Count);

        return result;
    }

    private void GenerateZonesFile(FileInfo fileName, List<string> domains, string format)
    {
        _logger.LogInformation("Gathered domains from all sources: {count}", domains.Count);
        _logger.LogInformation("Writing domains to file {fileName}", fileName);
        _logger.LogInformation("Using line format {format}", format);

        using var writer = new StreamWriter(fileName.FullName, false);

        foreach (var zone in domains)
        {
            var formattedZone = string.Format(format, zone);

            writer.WriteLine(formattedZone);
        }
    }

    private static string GetFormat(FormatType formatType, string format)
        => formatType switch
        {
            FormatType.Bind9 => _zoneFormat,
            FormatType.Hosts => _hostsFileFormat,
            FormatType.Custom => format,
            _ => throw new ArgumentOutOfRangeException(nameof(formatType))
        };
}
