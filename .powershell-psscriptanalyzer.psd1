# PSScriptAnalyzer (run by MegaLinter) for the scripts in scripts/.
@{
    Severity     = @('Error', 'Warning')
    ExcludeRules = @(
        # The scripts are interactive developer tools; coloured progress on the host is the point.
        'PSAvoidUsingWriteHost',
        # Private helpers inside a script that itself supports -WhatIf.
        'PSUseShouldProcessForStateChangingFunctions'
    )
}
