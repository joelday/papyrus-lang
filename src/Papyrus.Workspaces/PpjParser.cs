using System;
using System.Collections.Immutable;
using System.IO;
using System.Xml;
using Papyrus.Pex;

namespace Papyrus.Workspaces;

public static class PpjParser
{
    public static PapyrusProject Parse(string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);

        using var textReader = new StringReader(xml);
        using var reader = XmlReader.Create(textReader, CreateSettings());
        return ReadProject(reader);
    }

    public static PapyrusProject Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var reader = XmlReader.Create(stream, CreateSettings());
        return ReadProject(reader);
    }

    private static XmlReaderSettings CreateSettings()
    {
        // Prohibit DTD and disable resolution so untrusted .ppj input cannot XXE.
        return new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            IgnoreWhitespace = true,
        };
    }

    private static PapyrusProject ReadProject(XmlReader reader)
    {
        reader.MoveToContent();
        if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "PapyrusProject")
        {
            throw new InvalidDataException("Expected a PapyrusProject root element.");
        }

        GameId? game = null;
        string? output = null;
        string? flags = null;
        var optimize = false;
        var release = false;
        var final = false;
        AsmKind? asm = null;

        if (reader.MoveToFirstAttribute())
        {
            do
            {
                if (IsNamespaceAttribute(reader))
                {
                    continue;
                }

                switch (reader.LocalName)
                {
                    case "Game":
                        game = ParseGame(reader.Value);
                        break;
                    case "Output":
                        output = reader.Value;
                        break;
                    case "Flags":
                        flags = reader.Value;
                        break;
                    case "Optimize":
                        optimize = ParseBool(reader.Value);
                        break;
                    case "Release":
                        release = ParseBool(reader.Value);
                        break;
                    case "Final":
                        final = ParseBool(reader.Value);
                        break;
                    case "Asm":
                        asm = ParseAsm(reader.Value);
                        break;
                }
            }
            while (reader.MoveToNextAttribute());

            reader.MoveToElement();
        }

        var imports = ImmutableArray.CreateBuilder<string>();
        var folders = ImmutableArray.CreateBuilder<ProjectFolder>();
        var scripts = ImmutableArray.CreateBuilder<string>();
        var variables = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        var preBuild = ImmutableArray.CreateBuilder<string>();
        var postBuild = ImmutableArray.CreateBuilder<string>();

        if (reader.IsEmptyElement)
        {
            reader.Read();
        }
        else
        {
            reader.ReadStartElement();
            while (reader.NodeType != XmlNodeType.EndElement && reader.NodeType != XmlNodeType.None)
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.Skip();
                    continue;
                }

                switch (reader.LocalName)
                {
                    case "Imports":
                        ReadStringItems(reader, "Import", imports);
                        break;
                    case "Folders":
                        ReadFolders(reader, folders);
                        break;
                    case "Scripts":
                        ReadStringItems(reader, "Script", scripts);
                        break;
                    case "Variables":
                        ReadVariables(reader, variables);
                        break;
                    case "PreBuild":
                    case "PreBuildEvent":
                        ReadStringItems(reader, "Command", preBuild);
                        break;
                    case "PostBuild":
                    case "PostBuildEvent":
                        ReadStringItems(reader, "Command", postBuild);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }

            if (reader.NodeType == XmlNodeType.EndElement)
            {
                reader.ReadEndElement();
            }
        }

        // Pyro compatibility: a project without a Game attribute takes its game from
        // the Creation Kit flags file it names.
        game ??= InferGameFromFlags(flags);

        return new PapyrusProject(
            game,
            imports.ToImmutable(),
            folders.ToImmutable(),
            scripts.ToImmutable(),
            variables.ToImmutable(),
            output,
            flags,
            optimize,
            release,
            final,
            asm,
            preBuild.ToImmutable(),
            postBuild.ToImmutable());
    }

    private static void ReadStringItems(
        XmlReader reader,
        string itemName,
        ImmutableArray<string>.Builder builder)
    {
        if (reader.IsEmptyElement)
        {
            reader.Read();
            return;
        }

        reader.ReadStartElement();
        while (reader.NodeType != XmlNodeType.EndElement && reader.NodeType != XmlNodeType.None)
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == itemName)
            {
                builder.Add(reader.ReadElementContentAsString());
            }
            else
            {
                reader.Skip();
            }
        }

        if (reader.NodeType == XmlNodeType.EndElement)
        {
            reader.ReadEndElement();
        }
    }

    private static void ReadFolders(XmlReader reader, ImmutableArray<ProjectFolder>.Builder builder)
    {
        if (reader.IsEmptyElement)
        {
            reader.Read();
            return;
        }

        reader.ReadStartElement();
        while (reader.NodeType != XmlNodeType.EndElement && reader.NodeType != XmlNodeType.None)
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Folder")
            {
                var noRecurse = false;
                if (reader.MoveToFirstAttribute())
                {
                    do
                    {
                        if (reader.LocalName == "NoRecurse")
                        {
                            noRecurse = ParseBool(reader.Value);
                        }
                    }
                    while (reader.MoveToNextAttribute());

                    reader.MoveToElement();
                }

                builder.Add(new ProjectFolder(reader.ReadElementContentAsString(), noRecurse));
            }
            else
            {
                reader.Skip();
            }
        }

        if (reader.NodeType == XmlNodeType.EndElement)
        {
            reader.ReadEndElement();
        }
    }

    private static void ReadVariables(
        XmlReader reader,
        ImmutableDictionary<string, string>.Builder builder)
    {
        if (reader.IsEmptyElement)
        {
            reader.Read();
            return;
        }

        reader.ReadStartElement();
        while (reader.NodeType != XmlNodeType.EndElement && reader.NodeType != XmlNodeType.None)
        {
            if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Variable")
            {
                string? name = null;
                string? value = null;
                if (reader.MoveToFirstAttribute())
                {
                    do
                    {
                        switch (reader.LocalName)
                        {
                            case "Name":
                                name = reader.Value;
                                break;
                            case "Value":
                                value = reader.Value;
                                break;
                        }
                    }
                    while (reader.MoveToNextAttribute());

                    reader.MoveToElement();
                }

                reader.Skip();
                if (name is not null && value is not null)
                {
                    builder[name] = value;
                }
            }
            else
            {
                reader.Skip();
            }
        }

        if (reader.NodeType == XmlNodeType.EndElement)
        {
            reader.ReadEndElement();
        }
    }

    private static GameId? InferGameFromFlags(string? flags)
    {
        if (string.IsNullOrWhiteSpace(flags))
        {
            return null;
        }

        var fileName = flags[(flags.LastIndexOfAny(['/', '\\']) + 1)..];
        if (fileName.Equals("TESV_Papyrus_Flags.flg", StringComparison.OrdinalIgnoreCase))
        {
            // LE and SE share the flags file; SE is the default Skyrim dialect.
            return GameId.SkyrimSpecialEdition;
        }

        if (fileName.Equals("Institute_Papyrus_Flags.flg", StringComparison.OrdinalIgnoreCase))
        {
            return GameId.Fallout4;
        }

        return null;
    }

    private static GameId? ParseGame(string value)
    {
        if (value.Equals("sse", StringComparison.OrdinalIgnoreCase))
        {
            return GameId.SkyrimSpecialEdition;
        }

        if (value.Equals("fo4", StringComparison.OrdinalIgnoreCase))
        {
            return GameId.Fallout4;
        }

        if (value.Equals("tesv", StringComparison.OrdinalIgnoreCase)
            || value.Equals("tes5", StringComparison.OrdinalIgnoreCase))
        {
            return GameId.Skyrim;
        }

        if (value.Equals("starfield", StringComparison.OrdinalIgnoreCase)
            || value.Equals("sf1", StringComparison.OrdinalIgnoreCase))
        {
            return GameId.Starfield;
        }

        return null;
    }

    private static bool ParseBool(string value)
    {
        return value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
    }

    private static AsmKind? ParseAsm(string value)
    {
        if (value.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return AsmKind.None;
        }

        if (value.Equals("keep", StringComparison.OrdinalIgnoreCase))
        {
            return AsmKind.Keep;
        }

        if (value.Equals("only", StringComparison.OrdinalIgnoreCase))
        {
            return AsmKind.Only;
        }

        if (value.Equals("discard", StringComparison.OrdinalIgnoreCase))
        {
            return AsmKind.Discard;
        }

        return null;
    }

    private static bool IsNamespaceAttribute(XmlReader reader)
    {
        return reader.Prefix == "xmlns" || reader.LocalName == "xmlns";
    }
}
