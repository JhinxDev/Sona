# Parakeet native scheduler repair

The official NeMo-Speech.cpp 0.2.0 scheduler allocates copy metadata for all 65,536
possible graph nodes for each session: about 1.3 GB per scheduler. This source
patch keeps graph capacity and full-capacity graph overhead, but grows tensor
copy metadata from actual nodes+leaves. Failed allocation remains fail-closed.

Use provenance.json for exact source/DLL pins and licenses. scheduler-metadata.patch
is the unified diff after the 19 original NVIDIA patches. The packaged DLL has no
diagnostic logging and no custom zero-allocation workaround.

Rebuild with an existing Git, CMake >=3.26, Visual Studio 2022 C++ x64 toolchain and
Windows SDK (no CUDA toolkit). This script does not install prerequisites:

```powershell
./rebuild.ps1 -OutputDirectory C:/build/parakeet-repair -CMakePath cmake -GitPath git
```

Choose a fresh output directory. All downloads, sources and build outputs remain
there; existing directories are refused. Review/accept rebuilt bytes before
repinning the application: reproducible source does not imply identical PE bytes.

The app hashes the official archive and original ggml-base member, then hashes
and copies the bundled replacement into private staging. Its manifest records
patch identity and replacement SHA256. CPU/GPU original member hashes are distinct.
All official NVIDIA notices remain; bundled repair licenses accompany the replacement.
