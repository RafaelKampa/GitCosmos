using System.Text;
using GitCosmos.Application.Common.Exceptions;
using GitCosmos.Application.Common.Localization;
using GitCosmos.Application.DTOs.Requests.Repository;
using GitCosmos.Application.Interfaces.Git;

namespace GitCosmos.Infrastructure.Git;

public static class InteractiveRebase
{
    public static async Task<string> ExecuteAsync(string root, string target, IReadOnlyList<RebaseStep> steps, IGitMutationRunner writer, CancellationToken token)
    {
        if (steps.Count == 0 || steps.Any(step => !GitHistoryParser.IsSha(step.Sha) || step.Action is not ("pick" or "drop" or "squash" or "fixup")))
            throw new ValidationAppException(LocalizedText.Get("branch.invalid"));
        var folder = Directory.CreateTempSubdirectory("GitCosmos-rebase-");
        try
        {
            var todo = string.Join('\n', steps.Select(step => step.Action + " " + step.Sha)) + "\n";
            var file = Path.Combine(folder.FullName, OperatingSystem.IsWindows() ? "sequence.ps1" : "sequence.sh");
            var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(todo));
            string editor;
            if (OperatingSystem.IsWindows())
            {
                await File.WriteAllTextAsync(file, "[IO.File]::WriteAllText($args[0], [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('" + encoded + "')), [Text.UTF8Encoding]::new($false))", token);
                var powershell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe");
                editor = Quote(powershell) + " -NoProfile -NonInteractive -ExecutionPolicy Bypass -File " + Quote(file);
            }
            else
            {
                await File.WriteAllTextAsync(file, "#!/bin/sh\nprintf '%s' '" + todo + "' > \"$1\"\n", token);
                editor = "sh " + Quote(file);
            }
            var result = await writer.RunAsync(root, ["-c", "sequence.editor=" + editor, "-c", "core.editor=true", "rebase", "--interactive", "--no-autosquash", "--no-autostash", "--no-update-refs", "--no-rebase-merges", target], token);
            if (result.ExitCode != 0) throw new OperationFailedAppException(LocalizedText.Get("branch.operation_failed"));
            return target;
        }
        finally
        {
            // Only files allocated by this invocation are removed; no recursive cleanup of computed paths.
            foreach (var name in new[] { "sequence.ps1", "sequence.sh" }) File.Delete(Path.Combine(folder.FullName, name));
            folder.Delete();
        }
    }
    private static string Quote(string value) => "'" + value.Replace('\\', '/').Replace("'", "'\\''") + "'";
}
