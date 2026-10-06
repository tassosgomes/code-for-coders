#!/usr/bin/env bash
# Idempotent Cloud Agent bootstrap: OS toolchains, SPA dependencies, and NuGet restore.
# Long-running services belong in cloud-agent-start.sh.

set -euo pipefail

repository_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repository_root"

export DEBIAN_FRONTEND=noninteractive
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1

sudo_apt() {
  sudo DEBIAN_FRONTEND=noninteractive apt-get \
    -o Dpkg::Options::="--force-confdef" \
    -o Dpkg::Options::="--force-confold" \
    "$@"
}

sudo_apt update -y
sudo_apt install -y ca-certificates curl gnupg iptables fuse-overlayfs ripgrep

if ! command -v docker >/dev/null 2>&1 || ! docker compose version >/dev/null 2>&1; then
  sudo install -m 0755 -d /etc/apt/keyrings
  sudo curl --proto '=https' --tlsv1.2 -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
  sudo chmod a+r /etc/apt/keyrings/docker.asc
  echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "${UBUNTU_CODENAME:-$VERSION_CODENAME}") stable" \
    | sudo tee /etc/apt/sources.list.d/docker.list >/dev/null
  sudo_apt update -y
  sudo_apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
fi

if ! dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then
  sudo_apt install -y dotnet-sdk-10.0
fi

# SPA CI uses Node 24. react-router requires >= 22.22, and /exec-daemon/node is older.
node_prefix=/usr/local/lib/nodejs
if ! "$node_prefix/bin/node" -v 2>/dev/null | grep -q '^v24\.'; then
  node_tarball=node-v24.21.0-linux-x64.tar.xz
  tmp_tarball="$(mktemp)"
  curl --proto '=https' --tlsv1.2 -fsSL "https://nodejs.org/dist/v24.21.0/${node_tarball}" -o "$tmp_tarball"
  echo "fd8e59d5a511510f6a298afb548f18c7d2b1be404d8b4a27d94fbe49f56cb2d6  $tmp_tarball" | sha256sum -c -
  sudo mkdir -p /usr/local/lib
  sudo tar -xJf "$tmp_tarball" -C /usr/local/lib
  sudo rm -rf "$node_prefix"
  sudo mv "/usr/local/lib/node-v24.21.0-linux-x64" "$node_prefix"
  rm -f "$tmp_tarball"
fi
sudo tee /etc/profile.d/nodejs.sh >/dev/null <<'EOF'
export PATH="/usr/local/lib/nodejs/bin:${PATH}"
EOF
sudo chmod 644 /etc/profile.d/nodejs.sh
export PATH="/usr/local/lib/nodejs/bin:${PATH}"

if ! id -nG ubuntu | grep -qw docker; then
  sudo usermod -aG docker ubuntu
fi

sudo mkdir -p /etc/docker
sudo tee /etc/docker/daemon.json >/dev/null <<'EOF'
{
  "storage-driver": "fuse-overlayfs",
  "exec-opts": ["native.cgroupdriver=cgroupfs"],
  "features": {
    "containerd-snapshotter": false
  },
  "iptables": true,
  "ip6tables": false
}
EOF

if [[ ! -d /var/lib/docker/image/fuse-overlayfs ]]; then
  sudo mkdir -p /var/lib/docker/fuse-overlayfs/l
  sudo mkdir -p /var/lib/docker/image/fuse-overlayfs/imagedb/content/sha256
  sudo mkdir -p /var/lib/docker/image/fuse-overlayfs/imagedb/metadata/sha256
  sudo mkdir -p /var/lib/docker/image/fuse-overlayfs/layerdb/sha256
  sudo mkdir -p /var/lib/docker/image/fuse-overlayfs/layerdb/mounts
  sudo mkdir -p /var/lib/docker/image/fuse-overlayfs/distribution/sha256
  echo '{"Repositories":{}}' | sudo tee /var/lib/docker/image/fuse-overlayfs/repositories.json >/dev/null
fi

npm ci --ignore-scripts --prefix src/admin-spa
npm ci --ignore-scripts --prefix src/student-spa

dotnet tool restore
for solution in src/*/*.slnx; do
  dotnet restore "$solution"
done
