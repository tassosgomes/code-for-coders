"""Check storage consistency for workers consuming the shared media database."""

import json
import os
from pathlib import Path
import re
import subprocess
import unittest


ROOT = Path(__file__).resolve().parents[2]
COMPOSE_FILES = [
    "docker-compose.yml",
    "docker-compose.remote.yml",
    "docker-compose.komodo.apis.yml",
    "docker-compose.komodo.web.yml",
]


def compose_config(files, storage):
    required_variables = {
        name
        for file in COMPOSE_FILES
        for name in re.findall(r"\$\{([A-Z0-9_]+):\?", (ROOT / file).read_text())
    }
    environment = {
        **os.environ,
        **dict.fromkeys(required_variables, "test-value"),
        **storage,
    }
    command = ["docker", "compose", "--env-file", "/dev/null", "--profile", "*"]
    for file in files:
        command.extend(["-f", str(ROOT / file)])
    command.extend(["config", "--format", "json"])
    # Compose includes environment secrets in its output; keep them in memory.
    result = subprocess.run(command, env=environment, capture_output=True, text=True, check=False)
    if result.returncode:
        raise RuntimeError("Compose configuration could not be resolved")
    return json.loads(result.stdout)["services"]


class RemoteMediaConfigTests(unittest.TestCase):
    def test_local_and_deployed_workers_use_the_selected_shared_bucket_and_prefix(self):
        storage = {"AWS_MEDIA_BUCKET": "qa-shared-media", "AWS_MEDIA_OBJECT_PREFIX": "qa/media"}
        local = compose_config(["docker-compose.yml", "docker-compose.remote.yml"], storage)
        deployed = compose_config(["docker-compose.komodo.apis.yml", "docker-compose.komodo.web.yml"], storage)

        for stack_name, services in [("local", local), ("deployed", deployed)]:
            for service in ["media", "media-worker"]:
                with self.subTest(stack=stack_name, service=service):
                    config = services[service]["environment"]
                    self.assertEqual(config["AwsMedia__BucketName"], storage["AWS_MEDIA_BUCKET"])
                    self.assertEqual(config["AwsMedia__ObjectKeyPrefix"], storage["AWS_MEDIA_OBJECT_PREFIX"])
            edge = services["media-edge"]["environment"]
            self.assertEqual(edge["MEDIA_EDGE_S3_BUCKET"], storage["AWS_MEDIA_BUCKET"])
            self.assertEqual(edge["MEDIA_EDGE_S3_PREFIX"], storage["AWS_MEDIA_OBJECT_PREFIX"])

    def test_local_infrastructure_keeps_its_own_bucket(self):
        services = compose_config(["docker-compose.yml"], {"AWS_MEDIA_BUCKET": "qa-shared-media"})
        for service in ["media", "media-worker"]:
            self.assertEqual(services[service]["environment"]["AwsMedia__BucketName"], "code-for-coders-media")


if __name__ == "__main__":
    unittest.main()
