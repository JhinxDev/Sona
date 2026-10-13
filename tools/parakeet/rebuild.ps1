# Requires an existing CMake >=3.26, Git, Visual Studio2022 C++ x64 tools and Windows SDK.
# No CUDA toolkit is needed: only ggml-base is compiled. No tools are installed.
param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [string]$CMakePath = 'cmake',
    [string]$GitPath = 'git'
)
$ErrorActionPreference = 'Stop'
Get-Command $CMakePath -ErrorAction Stop | Out-Null
Get-Command $GitPath -ErrorAction Stop | Out-Null
$taskArea = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $taskArea) { throw 'Output directory exists; preserve it and choose a fresh directory.' }
New-Item -ItemType Directory -Path $taskArea | Out-Null
$downloads = @(
    @{Name='nemo-speech-v0.2.0-source.zip'; Url='https://github.com/NVIDIA/NeMo-Speech.cpp/archive/refs/tags/v0.2.0.zip'; Hash='6989d81b5cc895b809d1da1197fe7a01f07d7be7c41a18ab23ebca6278a02733'; Destination='source'},
    @{Name='llama-bd4f514-source.zip'; Url='https://github.com/ggml-org/llama.cpp/archive/bd4f514db14d87fded667787a7a963bfbaa98e89.zip'; Hash='d74b36880a15ad7e6889dc1bb1db7489e929e4110cc613b9716cfc31bc33bbe7'; Destination='upstream'}
)
foreach ($download in $downloads) {
    $archive = Join-Path $taskArea $download.Name
    Invoke-WebRequest -Uri $download.Url -OutFile $archive -TimeoutSec 1800
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath $archive).Hash -ne $download.Hash) { throw 'Pinned source archive differs.' }
    Expand-Archive -LiteralPath $archive -DestinationPath (Join-Path $taskArea $download.Destination)
}
$run = Join-Path $taskArea 'scheduler-repair'
$upstream = Join-Path $taskArea 'upstream/llama.cpp-bd4f514db14d87fded667787a7a963bfbaa98e89'
$nemo = Join-Path $taskArea 'source/NeMo-Speech.cpp-0.2.0'
$cmake = $CMakePath
foreach ($pin in @(
    @{File='nemo-speech-v0.2.0-source.zip'; Hash='6989d81b5cc895b809d1da1197fe7a01f07d7be7c41a18ab23ebca6278a02733'},
    @{File='llama-bd4f514-source.zip'; Hash='d74b36880a15ad7e6889dc1bb1db7489e929e4110cc613b9716cfc31bc33bbe7'})) {
    if ((Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $taskArea $pin.File)).Hash -ne $pin.Hash) { throw 'Source archive pin differs.' }
}
New-Item -ItemType Directory -Path $run | Out-Null
$tree = Join-Path $run 'llama'
Copy-Item -LiteralPath $upstream -Destination $tree -Recurse
$series = @(Get-Content -LiteralPath "$nemo/patches/series" | Where-Object { $_ -and -not $_.StartsWith('#') })
if ($series.Count -ne 19) { throw 'NVIDIA patch series differs.' }
foreach ($patch in $series) {
    & $GitPath -C $tree apply --whitespace=nowarn "$nemo/patches/$patch"
    if ($LASTEXITCODE) { throw "NVIDIA patch failed: $patch" }
}
$path = Join-Path $tree 'ggml/src/ggml-backend.cpp'
$source = [IO.File]::ReadAllText($path).Replace("`r`n", "`n")
[IO.File]::WriteAllText((Join-Path $run 'ggml-backend-before.cpp'), $source, [Text.UTF8Encoding]::new($false))
function Replace-ExactlyOnce([string]$Before, [string]$After) {
    if (($script:source.Split($Before).Length - 1) -ne 1) { throw 'Repair source anchor differs.' }
    $script:source = $script:source.Replace($Before, $After)
}
Replace-ExactlyOnce '    size_t context_buffer_size;' "    size_t context_buffer_size;`n    size_t context_graph_overhead;"
$before = @'
    struct ggml_init_params params = {
        /* .mem_size =   */ sched->context_buffer_size,
        /* .mem_buffer = */ sched->context_buffer,
        /* .no_alloc =   */ true
    };

    ggml_free(sched->ctx);

    sched->ctx = ggml_init(params);
'@
$after = @'
    // Reserve copy metadata for the actual graph, rather than every possible
    // node in the scheduler's capacity. The original per-node split/input
    // bound is retained; including leaves also covers their copied tensors.
    GGML_ASSERT(graph->n_nodes >= 0 && graph->n_leafs >= 0);
    const size_t n_nodes = (size_t) graph->n_nodes;
    const size_t n_leafs = (size_t) graph->n_leafs;
    GGML_ASSERT(n_nodes <= SIZE_MAX - n_leafs);
    const size_t tensor_count = n_nodes + n_leafs;
    static_assert(GGML_SCHED_MAX_SPLIT_INPUTS <= SIZE_MAX / (2 * sizeof(struct ggml_tensor)),
                  "scheduler metadata stride overflows");
    const size_t copy_stride = GGML_SCHED_MAX_SPLIT_INPUTS * 2 * sizeof(struct ggml_tensor);
    GGML_ASSERT(copy_stride > 0);
    GGML_ASSERT(tensor_count <= (SIZE_MAX - sched->context_graph_overhead) / copy_stride);
    const size_t required = tensor_count * copy_stride + sched->context_graph_overhead;

    // The old context refers to the buffer, so release it before resizing.
    // Allocate the replacement first: a failed growth preserves the old
    // buffer and stops before a null buffer can reach ggml_init.
    ggml_free(sched->ctx);
    sched->ctx = NULL;
    if (required > sched->context_buffer_size) {
        char * replacement = (char *) malloc(required);
        if (replacement == NULL) {
            GGML_ABORT("%s: failed to allocate scheduler metadata (%zu bytes)", __func__, required);
        }
        free(sched->context_buffer);
        sched->context_buffer = replacement;
        sched->context_buffer_size = required;
    }

    struct ggml_init_params params = {
        /* .mem_size =   */ sched->context_buffer_size,
        /* .mem_buffer = */ sched->context_buffer,
        /* .no_alloc =   */ true
    };

    sched->ctx = ggml_init(params);
'@
Replace-ExactlyOnce $before $after
$before = @'
    sched->context_buffer_size = ggml_sched_max_splits*GGML_SCHED_MAX_SPLIT_INPUTS*2*sizeof(struct ggml_tensor) + ggml_graph_overhead_custom(graph_size, false);
    sched->context_buffer = (char *) malloc(sched->context_buffer_size);
'@
$after = @'
    // Keep full graph capacity; allocate tensor-copy metadata lazily from the
    // actual graph at split time. Graph overhead retains the full capacity.
    sched->context_graph_overhead = ggml_graph_overhead_custom(graph_size, false);
    sched->context_buffer_size = 0;
    sched->context_buffer = NULL;
'@
Replace-ExactlyOnce $before $after
[IO.File]::WriteAllText($path, $source, [Text.UTF8Encoding]::new($false))
$diff = & $GitPath -c core.abbrev=7 -c core.autocrlf=false diff --no-index --no-ext-diff -- (Join-Path $run 'ggml-backend-before.cpp') $path
if ($LASTEXITCODE -ne 1) { throw 'Expected one source diff.' }
$diff = ($diff -join "`n") -replace '(?m)^diff --git .+$', 'diff --git a/ggml/src/ggml-backend.cpp b/ggml/src/ggml-backend.cpp' -replace '(?m)^--- .+$', '--- a/ggml/src/ggml-backend.cpp' -replace '(?m)^\+\+\+ .+$', '+++ b/ggml/src/ggml-backend.cpp'
[IO.File]::WriteAllText((Join-Path $run 'scheduler-metadata.patch'), $diff + "`n", [Text.UTF8Encoding]::new($false))
if ((Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $run 'scheduler-metadata.patch')).Hash -ne '65798b97ffd076d075fc4bac518320aad42c6e4562329d015fe393499cb69f7c') { throw 'Reconstructed source patch differs from the accepted pin.' }
$build = Join-Path $run 'build'
& $cmake -S $tree -B $build -G 'Visual Studio 17 2022' -A x64 -DBUILD_SHARED_LIBS=ON -DGGML_CPU=OFF -DGGML_CUDA=OFF -DGGML_NATIVE=OFF -DGGML_CCACHE=OFF -DGGML_BUILD_TESTS=OFF -DGGML_BUILD_EXAMPLES=OFF -DLLAMA_BUILD_TESTS=OFF -DLLAMA_BUILD_EXAMPLES=OFF -DLLAMA_BUILD_TOOLS=OFF -DLLAMA_CURL=OFF
if ($LASTEXITCODE) { throw 'Repair configure failed.' }
& $cmake --build $build --config Release --target ggml-base --parallel 4
if ($LASTEXITCODE) { throw 'Repair build failed.' }
Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $build 'bin/Release/ggml-base.dll')
