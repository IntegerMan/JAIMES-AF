# Stop running JAIMES AF applications (AppHost, ApiService, WebFrontend)
# Also attempts to disconnect VS Code debugger sessions
$workspace = $args[0]
if (-not $workspace) {
    Write-Host "Workspace path not provided"
    exit 1
}

# Check if VS Code/Cursor is running and might have active debug sessions
$vscodeProcesses = Get-Process -Name "Code", "Cursor" -ErrorAction SilentlyContinue
$hasVSCode = $vscodeProcesses -ne $null

if ($hasVSCode) {
    Write-Host "VS Code/Cursor detected. If you have an active debug session, stop it first (Shift+F5) to avoid attachment issues."
    Write-Host ""
}

$patterns = @('AppHost', 'ApiService', 'JAIMES AF.Web', 'JAIMES AF.AppHost', 'JAIMES AF.ApiService')
$stopped = $false

try {
    # Find and stop application processes
    $processes = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object {
        $cmd = $_.CommandLine
        if ($cmd) {
            $inWorkspace = $cmd -like "*$workspace*"
            $matchesPattern = $patterns | Where-Object { $cmd -like "*$_*" } | Select-Object -First 1
            $inWorkspace -and $matchesPattern
        }
    }

    if ($processes) {
        foreach ($proc in $processes) {
            try {
                # Get child processes (like debugger hosts) before stopping the main process
                $childProcs = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object {
                    $_.ParentProcessId -eq $proc.ProcessId
                }
                
                # Stop child processes first (debugger hosts, etc.)
                foreach ($childProc in $childProcs) {
                    try {
                        # Check if process still exists before trying to stop it
                        $procExists = Get-Process -Id $childProc.ProcessId -ErrorAction SilentlyContinue
                        if ($procExists -and ($childProc.Name -like '*vsdbg*' -or $childProc.Name -like '*debugger*')) {
                            Stop-Process -Id $childProc.ProcessId -Force -ErrorAction Stop
                            Write-Host "Stopped debugger process $($childProc.ProcessId): $($childProc.Name)"
                        }
                    }
                    catch {
                        # Ignore errors - process may have already terminated
                    }
                }
                
                # Check if main process still exists before trying to stop it
                $mainProcExists = Get-Process -Id $proc.ProcessId -ErrorAction SilentlyContinue
                if ($mainProcExists) {
                    Stop-Process -Id $proc.ProcessId -Force -ErrorAction Stop
                    Write-Host "Stopped process $($proc.ProcessId): $($proc.Name)"
                    $stopped = $true
                }
            }
            catch {
                # Only show error if it's not a "process not found" error
                if ($_.Exception.Message -notlike '*Cannot find a process*') {
                    Write-Host "Could not stop process $($proc.ProcessId): $_"
                }
            }
        }
        if ($stopped) {
            Write-Host "Stopped running JAIMES AF applications"
        }
    }
    else {
        Write-Host "No running JAIMES AF applications found"
    }
    
    # Also try to find and stop any orphaned debugger processes related to this workspace
    $debuggerProcs = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue | Where-Object {
        $cmd = $_.CommandLine
        if ($cmd) {
            ($_.Name -like '*vsdbg*' -or $_.Name -like '*debugger*') -and 
            ($cmd -like "*$workspace*" -or $cmd -like "*AppHost*" -or $cmd -like "*ApiService*")
        }
    }
    
    if ($debuggerProcs) {
        foreach ($dbgProc in $debuggerProcs) {
            try {
                # Check if process still exists before trying to stop it
                $procExists = Get-Process -Id $dbgProc.ProcessId -ErrorAction SilentlyContinue
                if ($procExists) {
                    Stop-Process -Id $dbgProc.ProcessId -Force -ErrorAction Stop
                    Write-Host "Stopped orphaned debugger process $($dbgProc.ProcessId): $($dbgProc.Name)"
                }
            }
            catch {
                # Ignore errors - process may have already terminated
            }
        }
    }
    
    # Note: VS Code debugger sessions are managed internally by VS Code
    # When processes are killed externally, VS Code should auto-disconnect, but sometimes doesn't
    # If the debugger still shows as attached, manually disconnect using:
    # - Click the Stop button in the Debug toolbar, or
    # - Press Shift+F5, or
    # - Command Palette (Ctrl+Shift+P) -> "Debug: Stop Debugging"
    if ($stopped) {
        Write-Host ""
        Write-Host "Note: If VS Code still shows the debugger as attached, manually disconnect it using Shift+F5 or the Stop button in the Debug toolbar."
    }
}
catch {
    Write-Host "Error stopping applications: $_"
    exit 1
}

