@{
    # PSScriptAnalyzer settings for LibreSpot.
    # Suppressions are project-specific: the PS1 script uses unapproved verbs
    # deliberately (Module-*, Apply-*, Capture-*, Load-, etc.) and relies on
    # Write-Host for WPF GUI log output.

    Severity = @('Error', 'Warning')

    ExcludeRules = @(
        # LibreSpot uses Module-NukeSpotify, Module-InstallSpotX, Apply-ConfigToUi,
        # Capture-CustomConfigBaseline, Load-LibreSpotConfig, Download-FileSafe,
        # Normalize-LibreSpotConfig, Download-CommunityExtensions, Reapply-*,
        # Check-ForUpdates — all deliberate naming for a single-file app.
        'PSUseApprovedVerbs',

        # Write-Host is the primary GUI log output mechanism in the PS1 script.
        # The WPF shell uses proper logging (Serilog); the PS1 script intentionally
        # writes to host for DispatcherTimer polling.
        'PSAvoidUsingWriteHost',

        # Single-file script with 129 functions; the aliases PSScriptAnalyzer
        # flags (e.g., ForEach-Object %) are used sparingly and intentionally.
        'PSAvoidUsingCmdletAliases',

        # The script uses ShouldProcess on mutating helpers but not universally.
        # Full ShouldProcess coverage is a future roadmap item (Cycle 19).
        'PSUseShouldProcessForStateChangingFunctions',

        # Positional parameters are used pervasively in the single-file script.
        'PSAvoidUsingPositionalParameters',

        # Global variables are the primary state mechanism for the single-file
        # PS1 script (config paths, pinned versions, UI hash table). The WPF
        # backend also uses globals for cross-function state. Refactoring to
        # module-scoped variables is part of the shared-core extraction (P1).
        'PSAvoidGlobalVars',

        # Plural nouns are used deliberately: Build-SpotXParams builds a list
        # of parameters, Get-PathEntries returns multiple entries, etc.
        'PSUseSingularNouns',

        # The PS1 declares unused variables for WPF data binding. The backend
        # similarly pre-declares state variables.
        'PSUseDeclaredVarsMoreThanAssignments',

        # Write-Log is a project function, not an override of a built-in cmdlet.
        # It exists in PowerShell Core but not in Windows PowerShell 5.1.
        'PSAvoidOverwritingBuiltInCmdlets',

        # BOM encoding: the project uses BOM-free UTF-8 consistently.
        'PSUseBOMForUnicodeEncodedFile',

        # Empty catch blocks are used deliberately for best-effort cleanup,
        # optional feature probing, and UI resilience (e.g., icon loading,
        # window flash, clipboard, process termination). Swallowing exceptions
        # in these contexts is the intended behavior.
        'PSAvoidUsingEmptyCatchBlock',

        # Several parameters are used by WPF data-binding or by the worker
        # runspace but appear unused from PSScriptAnalyzer's perspective.
        'PSReviewUnusedParameter'
    )

    Rules = @{
        PSUseCompatibleSyntax = @{
            Enable         = $true
            TargetVersions = @('5.1', '7.6')
        }
    }
}
