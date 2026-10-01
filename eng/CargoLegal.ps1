# Shared source binding for build, staging and package validation.
# tar is available on all three qualified build platforms (including Windows).
$script:CargoLegalCache = @{}

function Read-CrateBytes([string]$Archive, [string]$Member) {
    $start = New-Object System.Diagnostics.ProcessStartInfo
    $start.FileName = (Get-Command tar -ErrorAction Stop).Source
    # Both values originate from local paths / validated archive entries. Invoke
    # the executable directly, never a shell; quote whitespace without evaluation.
    if ($Archive.Contains('"') -or $Member.Contains('"')) { throw "Invalid crate path." }
    $start.Arguments = '-xOf "' + $Archive + '" "' + $Member + '"'
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $start
    $buffer = New-Object System.IO.MemoryStream
    try {
        [void]$process.Start()
        $errorRead = $process.StandardError.ReadToEndAsync()
        $process.StandardOutput.BaseStream.CopyTo($buffer)
        $process.WaitForExit()
        $errorText = $errorRead.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) { throw "Cannot read verified crate entry: $errorText" }
        return ,$buffer.ToArray()
    } finally { $buffer.Dispose(); $process.Dispose() }
}

function Get-CrateLegalMaterials($Package, [string]$LockText) {
    $slug = "$($Package.name)-$($Package.version)"
    if ($script:CargoLegalCache.ContainsKey($slug)) { return $script:CargoLegalCache[$slug] }
    $pattern = '(?ms)^\[\[package\]\]\r?\n(?:(?!\[\[package\]\]).)*?^name = "' + [regex]::Escape($Package.name) + '"\r?\nversion = "' + [regex]::Escape($Package.version) + '"(?:(?!\[\[package\]\]).)*?^checksum = "([0-9a-f]{64})"'
    $match = [regex]::Match($LockText, $pattern)
    if (-not $match.Success) { throw "Cargo.lock registry checksum missing for $slug" }
    $checksum = $match.Groups[1].Value
    $archive = Get-ChildItem -LiteralPath (Join-Path $HOME ".cargo/registry/cache") -Filter "$slug.crate" -File -Recurse -ErrorAction Stop |
        Where-Object { (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() -eq $checksum } |
        Select-Object -First 1
    if ($null -eq $archive) { throw "Cached registry archive checksum mismatch or missing for $slug" }
    $members = @(& tar -tzf $archive.FullName)
    if ($LASTEXITCODE -ne 0) { throw "Cannot list verified crate $slug" }
    $materials = @()
    $seen = New-Object 'System.Collections.Generic.HashSet[string]' ([StringComparer]::Ordinal)
    foreach ($member in $members) {
        if ($member.EndsWith('/')) { continue }
        if (-not $member.StartsWith("$slug/", [StringComparison]::Ordinal)) { throw "Crate entry outside expected root: $member" }
        $relative = $member.Substring($slug.Length + 1)
        if ($relative -match '(^|/)\.\.?(/|$)|[\\":]' -or $relative.StartsWith('/')) { throw "Unsafe crate entry: $member" }
        $name = ($relative -split '/')[-1]
        if ($name -notmatch '^(LICENSE|LICENCE|COPYING|NOTICE|COPYRIGHT)(\.|$|-)' -and $relative -ne $Package.license_file) { continue }
        if (-not $seen.Add($relative)) { throw "Duplicate crate legal entry: $member" }
        $bytes = Read-CrateBytes $archive.FullName $member
        $hasher = [Security.Cryptography.SHA256]::Create()
        try { $hash = ([BitConverter]::ToString($hasher.ComputeHash($bytes))).Replace('-', '').ToLowerInvariant() }
        finally { $hasher.Dispose() }
        $materials += [pscustomobject]@{ source_path = $relative; path = "dependencies/$slug/$relative"; sha256 = $hash; bytes = $bytes }
    }
    if ($materials.Count -eq 0) { throw "No legal material found in verified crate $slug" }
    $result = [pscustomobject]@{ archive_sha256 = $checksum; materials = $materials }
    $script:CargoLegalCache[$slug] = $result
    return $result
}

function Test-CrateLegalMaterials($Entry, $Package, [string]$LockText, [string]$LegalRoot) {
    if ($Entry.name -ne $Package.name -or $Entry.version -ne $Package.version -or
        $Entry.source -ne $Package.source -or $Entry.declared_license -ne $Package.license) {
        throw "Legal source/license metadata differs from Cargo: $($Entry.package_id)"
    }
    $source = Get-CrateLegalMaterials $Package $LockText
    if ($Entry.registry_archive_sha256 -ne $source.archive_sha256) { throw "Legal source checksum mismatch: $($Entry.package_id)" }
    $expected = @($source.materials | ForEach-Object { $_.source_path } | Sort-Object)
    $actual = @($Entry.license_material | ForEach-Object { $_.source_path } | Sort-Object)
    if (($expected -join '|') -cne ($actual -join '|')) { throw "Legal source coverage mismatch: $($Entry.package_id)" }
    foreach ($material in $source.materials) {
        $listed = @($Entry.license_material | Where-Object { $_.source_path -ceq $material.source_path })
        if ($listed.Count -ne 1 -or $listed[0].path -cne $material.path) { throw "Legal source path mismatch: $($material.source_path)" }
        $path = Join-Path $LegalRoot $material.path
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing legal material: $($material.path)" }
        $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($hash -ne $listed[0].sha256) { throw "Legal material hash mismatch: $($material.path)" }
        if ($hash -ne $material.sha256) { throw "Legal archive content mismatch: $($material.path)" }
    }
}
