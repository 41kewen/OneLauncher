using OneLauncher.Core.Helper.Models;
using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using OneLauncher.Core.Global;
using OneLauncher.Core.DefaultComponent.Mod.ModManager;

namespace OneLauncher.Core.DefaultComponent.Resource.Mod.Manager;
internal class FabricModInfoHelper : IModInfoHelper 
{
    public async Task<ModInfo?> GetModInfoAsync(string filePath, ZipArchive archive, ZipArchiveEntry configEntry)
    {
        FabricModJson? parsedInfo;
        try
        {
            await using Stream configStream = configEntry.Open();
            parsedInfo = await JsonSerializer.DeserializeAsync(
                configStream, FabricModJsonContext.Default.FabricModJson);
        }
        catch (JsonException)
        {
            // 部分模组将实际换行（或反斜杠后接换行）写入 JSON 字符串。
            // 仅在严格解析失败后修复字符串内的换行，不改动 JAR 内容。
            using StreamReader reader = new(configEntry.Open(), Encoding.UTF8);
            string json = await reader.ReadToEndAsync();
            parsedInfo = JsonSerializer.Deserialize(
                EscapeStringLineBreaks(json), FabricModJsonContext.Default.FabricModJson);
        }

        FabricModJson info = parsedInfo ?? throw new OlanException("模组信息解析失败", $"读取模组 JAR 的 fabric.mod.json 条目；文件/目录 filePath={filePath}。无法从文件'{filePath}'的fabric.mod.json中读取到有效信息");

        byte[]? iconBytes = null;
        if (info.Icon != null)
        {
            // 使用传入的 archive 来提取图标
            iconBytes = await ExtractIconAsync(archive, info.Icon);
        }

        return new ModInfo
        {
            Id = info.Id ?? "未知ID",
            Version = info.Version ?? "未知版本",
            Name = info.Name ?? Path.GetFileNameWithoutExtension(filePath),
            Description = info.Description ?? "无描述",
            Icon = iconBytes,
            IsEnabled = !filePath.EndsWith(".disabled"),
            fileName = Path.GetFileName(filePath)
        };
    }

    private static string EscapeStringLineBreaks(string json)
    {
        StringBuilder result = new(json.Length);
        bool inString = false;
        for (int i = 0; i < json.Length; i++)
        {
            char current = json[i];
            if (inString && current == '\\' && i + 1 < json.Length)
            {
                char next = json[i + 1];
                if (next is '\r' or '\n')
                {
                    // 丢弃无效的续行反斜杠，由下方将实际换行转义。
                    current = json[++i];
                }
                else
                {
                    // 保留已有转义，避免将转义引号误判为字符串结束。
                    result.Append(current).Append(next);
                    i++;
                    continue;
                }
            }

            if (inString && current is '\r' or '\n')
            {
                result.Append(current == '\r' ? "\\r" : "\\n");
            }
            else
            {
                if (current == '"') inString = !inString;
                result.Append(current);
            }
        }

        return result.ToString();
    }

    private async Task<byte[]?> ExtractIconAsync(ZipArchive archive, string iconPath)
    {
        if (string.IsNullOrEmpty(iconPath)) return null;

        ZipArchiveEntry? iconEntry = archive.GetEntry(iconPath);
        if (iconEntry == null) return null;

        await using Stream iconStream = iconEntry.Open();
        using MemoryStream memoryStream = new();
        await iconStream.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }
}
