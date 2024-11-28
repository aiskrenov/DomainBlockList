using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Threading.Tasks;

namespace DomainBlockList;

internal class Program
{
    static async Task<int> Main(string[] args)
    {
        var fileOption = new Option<FileInfo>(
            name: "--output",
            description: "The output file, containing all formatted domains.",
            getDefaultValue: () => new FileInfo($"{AppContext.BaseDirectory}named.conf.blocks"));

        var formatTypeOption = new Option<FormatType>(
            name: "--type",
            description: "Type of the format used for each line. Possible values are Bind9, Hosts, and Custom (--format can be provided to specify the format).",
            getDefaultValue: () => FormatType.Bind9);

        var formatOption = new Option<string>(
            name: "--format",
            description: "Custom string format of each line. The format must include the {0} placeholder for the domain name.",
            getDefaultValue: () => "{0}");

        var rootCommand = new RootCommand("Domain block list generator for Bind9, hosts file, Pi-hole, etc.")
        {
            fileOption,
            formatTypeOption,
            formatOption
        };

        rootCommand.SetHandler(GenerateOutput, fileOption, formatTypeOption, formatOption);
        return await rootCommand.InvokeAsync(args);
    }

    internal static async Task GenerateOutput(FileInfo file, FormatType formatType, string format)
    {
        using var factory = LoggerFactory.Create(builder => builder.AddConsole());

        var logger = factory.CreateLogger("Domain Block List Compiler");

        var sources = LoadFile(logger, "sources");
        var localBlockList = LoadFile(logger, "local-block-list");

        var compiler = new DomainBlockCompiler(logger, sources, localBlockList);
        await compiler.GenerateZonesAsync(file, formatType, format);
    }

    internal static List<string> LoadFile(ILogger logger, string fileName)
    {
        var result = new List<string>();

        try
        {
            using var sources = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            using var reader = new StreamReader(sources);

            var line = string.Empty;
            while ((line = reader.ReadLine()) != null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    result.Add(line);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load file: {fileName}", fileName);
            throw;
        }

        return result;
    }
}
