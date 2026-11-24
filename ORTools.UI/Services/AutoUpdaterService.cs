using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace ORTools.UI.Services;

public static class AutoUpdaterService
{
    private static readonly HttpClient _http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ORTools", "Updater"));
        return client;
    }

    /// <summary>
    /// Downloads the release zip, stages extraction (preserving user Profiles/Config),
    /// and launches a detached swap-and-restart helper script.
    /// Caller should immediately exit the app if this returns without throwing.
    /// </summary>
    public static async Task DownloadAndStageUpdateAsync(
        string zipUrl,
        IProgress<(double Percent, string Status)>? progress = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(zipUrl))
            throw new InvalidOperationException("No download URL available for this release.");

        string stageRoot = Path.Combine(Path.GetTempPath(), $"ORTools_Update_{Guid.NewGuid():N}");
        string zipPath = Path.Combine(stageRoot, "update.zip");
        string extractDir = Path.Combine(stageRoot, "extracted");
        bool helperLaunched = false;

        try
        {
            Directory.CreateDirectory(stageRoot);

            progress?.Report((5, "Connecting..."));

            using (var response = await _http.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false))
            {
                response.EnsureSuccessStatusCode();
                long? totalBytes = response.Content.Headers.ContentLength;

                await using var contentStream = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
                await using var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);

                var buffer = new byte[81920];
                long totalRead = 0;
                int read;

                while ((read = await contentStream.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                    totalRead += read;

                    if (totalBytes.HasValue && totalBytes.Value > 0)
                    {
                        double pct = Math.Min(85.0, (double)totalRead / totalBytes.Value * 85.0);
                        progress?.Report((pct, $"Downloading... {pct / 0.85:0}%"));
                    }
                    else
                    {
                        progress?.Report((40, $"Downloading... {totalRead / 1024} KB"));
                    }
                }
            }

            progress?.Report((90, "Extracting update..."));
            ZipFile.ExtractToDirectory(zipPath, extractDir, overwriteFiles: true);

            string? exeFile = Directory.GetFiles(extractDir, "*.exe", SearchOption.AllDirectories)
                .FirstOrDefault(f => Path.GetFileName(f).Contains("ORTools", StringComparison.OrdinalIgnoreCase))
                ?? Directory.GetFiles(extractDir, "*.exe", SearchOption.AllDirectories).FirstOrDefault();

            if (exeFile == null)
                throw new InvalidOperationException("Downloaded archive does not contain an executable.");

            string sourceDir = Path.GetDirectoryName(exeFile)!;
            string? processPath = Environment.ProcessPath;
            string targetDir = (!string.IsNullOrEmpty(processPath) ? Path.GetDirectoryName(processPath) : null)
                               ?? AppContext.BaseDirectory;
            targetDir = targetDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            string stagedExeName = Path.GetFileName(exeFile);
            string targetExeName = !string.IsNullOrEmpty(processPath)
                ? Path.GetFileName(processPath)
                : stagedExeName;

            // If the user renamed their executable (e.g. OSROTools-HR.exe alongside OSROTools-MR.exe),
            // rename the staged exe so the copy step overwrites the running binary in place.
            if (!string.Equals(stagedExeName, targetExeName, StringComparison.OrdinalIgnoreCase))
            {
                string renamedStagedExe = Path.Combine(sourceDir, targetExeName);
                File.Move(exeFile, renamedStagedExe, overwrite: true);
            }

            // Never overwrite user Profiles or Config folders
            foreach (string protectedDir in new[] { "Profiles", "Config" })
            {
                string stagedProtected = Path.Combine(sourceDir, protectedDir);
                if (Directory.Exists(stagedProtected))
                    Directory.Delete(stagedProtected, recursive: true);
            }

            progress?.Report((96, "Restarting..."));

            int pid = Environment.ProcessId;
            string scriptPath = Path.Combine(stageRoot, "apply_update.ps1");

            string Esc(string s) => s.Replace("'", "''");

            string psScript = $@"
$pidToWait = {pid}
$source = '{Esc(sourceDir)}'
$target = '{Esc(targetDir)}'
$exePath = '{Esc(Path.Combine(targetDir, targetExeName))}'
$stageRoot = '{Esc(stageRoot)}'

for ($i = 0; $i -lt 80; $i++) {{
    if (-not (Get-Process -Id $pidToWait -ErrorAction SilentlyContinue)) {{ break }}
    Start-Sleep -Milliseconds 250
}}
Start-Sleep -Milliseconds 400

$copyOk = $false
for ($attempt = 0; $attempt -lt 5; $attempt++) {{
    try {{
        Get-ChildItem -Path $source -Force -ErrorAction Stop | ForEach-Object {{
            if ($_.Name -ieq 'Profiles' -or $_.Name -ieq 'Config') {{ return }}
            $dest = Join-Path $target $_.Name
            if ($_.PSIsContainer) {{
                Copy-Item -Path $_.FullName -Destination $dest -Recurse -Force -ErrorAction Stop
            }} else {{
                Copy-Item -Path $_.FullName -Destination $dest -Force -ErrorAction Stop
            }}
        }}
        $copyOk = $true
        break
    }} catch {{
        Start-Sleep -Milliseconds 600
    }}
}}

if (-not $copyOk) {{
    Add-Type -AssemblyName System.Windows.Forms -ErrorAction SilentlyContinue
    [System.Windows.Forms.MessageBox]::Show(
        'OSRO Tools could not overwrite locked files during the update. Please download the update zip manually from GitHub Releases.',
        'OSRO Tools Update Failed',
        [System.Windows.Forms.MessageBoxButtons]::OK,
        [System.Windows.Forms.MessageBoxIcon]::Warning) | Out-Null
}}

if (Test-Path $exePath) {{
    try {{
        Start-Process -FilePath $exePath -WorkingDirectory $target -ErrorAction Stop
    }} catch {{
        Start-Process -FilePath $exePath -WorkingDirectory $target -Verb RunAs -ErrorAction SilentlyContinue
    }}
}}

if ($copyOk) {{
    Start-Sleep -Seconds 2
    Remove-Item -Path $stageRoot -Recurse -Force -ErrorAction SilentlyContinue
}}
";
            await File.WriteAllTextAsync(scriptPath, psScript, ct).ConfigureAwait(false);

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process.Start(psi);
            helperLaunched = true;
        }
        finally
        {
            if (!helperLaunched)
            {
                try
                {
                    if (Directory.Exists(stageRoot))
                        Directory.Delete(stageRoot, recursive: true);
                }
                catch { }
            }
        }
    }
}
