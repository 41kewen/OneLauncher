#!/usr/bin/env bash
set -euo pipefail

if [[ $# -gt 1 || "${1:-}" == "--help" || "${1:-}" == "-h" ]]; then
    echo "Usage: $0 [output-directory]"
    echo "Build a self-contained linux-x64 archive (default: artifacts/linux-x64)."
    exit 0
fi

# Platform symbols in the projects follow the build host, so publish on Linux.
if [[ "$(uname -s)" != "Linux" ]]; then
    echo "Build the Linux package on Linux (or in WSL)." >&2
    exit 1
fi

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
output_dir="${1:-$repo_root/artifacts/linux-x64}"
mkdir -p -- "$output_dir"
output_dir="$(cd -- "$output_dir" && pwd)"
staging_dir="$(mktemp -d "$output_dir/.onelauncher-build.XXXXXX")"
trap 'rm -rf -- "$staging_dir"' EXIT

package_name="OneLauncher-linux-x64"
package_dir="$staging_dir/$package_name"
archive_name="$package_name.tar.gz"

# Run from the repository so dotnet uses the SDK pinned in global.json.
cd -- "$repo_root"
dotnet publish OneLauncher.Desktop/OneLauncher.Desktop.csproj \
    --configuration Release \
    --runtime linux-x64 \
    --self-contained true \
    --output "$package_dir" \
    -p:UseAppHost=true \
    -p:PublishAot=false \
    -p:PublishTrimmed=false \
    -p:PublishSingleFile=false \
    -p:DebugType=None \
    -p:DebugSymbols=false

chmod +x "$package_dir/OneLauncher.Desktop"
cp README.md Terms_of_Service.md Privacy_Policy.md "$package_dir/"
tar -czf "$staging_dir/$archive_name" -C "$staging_dir" "$package_name"
mv -f -- "$staging_dir/$archive_name" "$output_dir/$archive_name"
(
    cd -- "$output_dir"
    sha256sum "$archive_name" > "$archive_name.sha256"
)

echo "Package: $output_dir/$archive_name"
echo "Checksum: $output_dir/$archive_name.sha256"
