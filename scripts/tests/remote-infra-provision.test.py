"""Check that provision never rotates credentials shared with the deployed stack."""

import os
from pathlib import Path
import stat
import subprocess
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "scripts" / "remote-infra.sh"

# Stands in for the infra server: answers the read-only queries and records every mutation.
FAKE_SSH = r"""#!/usr/bin/env bash
while [[ $1 == -o ]]; do shift 2; done
shift
command="$*"
case "$command" in
  true) ;;
  *pg_roles*) if [[ $FAKE_USERS_EXIST == 1 ]]; then echo 1; fi ;;
  *list_users*) if [[ $FAKE_USERS_EXIST == 1 ]]; then printf 'code_for_coders\t[management]\ncode_for_coders_bff_admin\t[]\n'; fi ;;
  *MINIO_ROOT*) printf 'MINIO_ROOT_USER=root\nMINIO_ROOT_PASSWORD=rootpassword\n' ;;
  *psql*) cat >/dev/null; echo "MUTATION postgres" >> "$FAKE_LOG" ;;
  "bash -s") cat >/dev/null; echo "MUTATION rabbitmq" >> "$FAKE_LOG" ;;
  *) echo "unexpected ssh: $command" >&2; exit 99 ;;
esac
"""

FAKE_DOCKER = r"""#!/usr/bin/env bash
if [[ "$*" == *"admin user info"* ]]; then
  [[ $FAKE_USERS_EXIST == 1 ]]
  exit
fi
if [[ "$*" == *"mc admin user add"* ]]; then
  echo "MUTATION minio ${*: -1}" >> "$FAKE_LOG"
  exit 0
fi
echo "unexpected docker: $*" >&2
exit 99
"""

SECRETS = {
    "REMOTE_DB_PASSWORD": "db-secret",
    "REMOTE_RABBITMQ_PASSWORD": "rabbit-secret",
    "BFF_ADMIN_RABBITMQ_PASSWORD": "bff-rabbit-secret",
    "REMOTE_S3_SECRET_KEY": "s3-secret",
    "REMOTE_VALKEY_PASSWORD": "valkey-secret",
}


class RemoteInfraProvisionTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        for name, content in [("ssh", FAKE_SSH), ("docker", FAKE_DOCKER)]:
            path = self.root / name
            path.write_text(content)
            path.chmod(path.stat().st_mode | stat.S_IXUSR)
        self.env_file = self.root / ".env"
        self.log = self.root / "mutations.log"
        self.log.touch()

    def provision(self, env_values, users_exist):
        self.env_file.write_text("".join(f"{key}={value}\n" for key, value in env_values.items()))
        environment = {
            **os.environ,
            "PATH": f"{self.root}{os.pathsep}{os.environ['PATH']}",
            "REMOTE_INFRA_ENV_FILE": str(self.env_file),
            "FAKE_LOG": str(self.log),
            "FAKE_USERS_EXIST": "1" if users_exist else "0",
        }
        return subprocess.run(
            [str(SCRIPT), "provision"], env=environment, capture_output=True, text=True, check=False
        )

    def test_missing_secrets_for_existing_users_abort_before_any_change(self):
        result = self.provision({"REMOTE_VALKEY_PASSWORD": "valkey-secret"}, users_exist=True)

        self.assertNotEqual(result.returncode, 0)
        for key in ["REMOTE_DB_PASSWORD", "REMOTE_RABBITMQ_PASSWORD", "BFF_ADMIN_RABBITMQ_PASSWORD", "REMOTE_S3_SECRET_KEY"]:
            self.assertIn(key, result.stderr)
            self.assertNotIn(f"{key}=", self.env_file.read_text())
        self.assertEqual(self.log.read_text(), "")

    def test_present_secrets_are_reapplied_unchanged(self):
        result = self.provision(SECRETS, users_exist=True)

        self.assertEqual(result.returncode, 0, result.stderr)
        mutations = self.log.read_text()
        self.assertIn("MUTATION postgres", mutations)
        self.assertIn("MUTATION rabbitmq", mutations)
        self.assertIn("mc admin user add infra 'code_for_coders_media' 's3-secret'", mutations)

    def test_first_provision_generates_secrets_for_new_users(self):
        result = self.provision({"REMOTE_VALKEY_PASSWORD": "valkey-secret"}, users_exist=False)

        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertIn("REMOTE_S3_SECRET_KEY=", self.env_file.read_text())
        self.assertIn("MUTATION minio", self.log.read_text())


if __name__ == "__main__":
    unittest.main()
