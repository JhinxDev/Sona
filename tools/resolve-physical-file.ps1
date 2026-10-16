# A packaged caller can see an AppData alias that an Explorer child cannot see.
# Resolve existing file identity before passing the path between those processes.
if(-not ('Sona.Launch.PhysicalFileNative' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Text;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace Sona.Launch {
    public static class PhysicalFileNative {
        [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
        public static extern uint GetFinalPathNameByHandle(SafeFileHandle file, StringBuilder path, uint size, uint flags);
    }
}
'@
}
function Resolve-SonaPhysicalFile([string]$Path) {
    $file=[IO.File]::OpenRead([IO.Path]::GetFullPath($Path))
    try {
        $buffer=[Text.StringBuilder]::new(1024)
        $length=[Sona.Launch.PhysicalFileNative]::GetFinalPathNameByHandle($file.SafeFileHandle,$buffer,[uint32]$buffer.Capacity,0)
        if($length -ge $buffer.Capacity -and $length -lt 32768) {
            $buffer=[Text.StringBuilder]::new([int]$length+1)
            $length=[Sona.Launch.PhysicalFileNative]::GetFinalPathNameByHandle($file.SafeFileHandle,$buffer,[uint32]$buffer.Capacity,0)
        }
        if($length -eq 0 -or $length -ge $buffer.Capacity){throw 'The existing launch file has no bounded physical path.'}
        $resolved=$buffer.ToString()
        if($resolved.StartsWith('\\?\UNC\',[StringComparison]::OrdinalIgnoreCase)){return '\\'+$resolved.Substring(8)}
        if($resolved.StartsWith('\\?\',[StringComparison]::OrdinalIgnoreCase)){return $resolved.Substring(4)}
        throw 'Windows returned an unsupported physical launch path.'
    } finally {$file.Dispose()}
}
