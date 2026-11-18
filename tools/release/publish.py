import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile


MANIFEST_NAME = "masterplayer.json"


def run(*args, **kwargs):
    return subprocess.run(args, check=True, text=True, **kwargs)


def release_view(repository, tag):
    result = subprocess.run(
        ("gh", "release", "view", tag, "--repo", repository, "--json", "isDraft,targetCommitish"),
        capture_output=True, text=True,
    )
    if result.returncode == 1 and result.stderr.strip() == "release not found":
        return None
    result.check_returncode()
    return json.loads(result.stdout)


def tag_target(tag):
    ref = f"refs/tags/{tag}"
    result = run("git", "ls-remote", "origin", ref, f"{ref}^{{}}", capture_output=True)
    refs = dict(reversed(line.split()) for line in result.stdout.splitlines())
    return refs.get(f"{ref}^{{}}", refs.get(ref))


def verify_assets(directory, zip_name, version, download_url):
    manifest_bytes = (directory / MANIFEST_NAME).read_bytes()
    entry, = json.loads(manifest_bytes)
    with (directory / zip_name).open("rb") as archive:
        checksum = hashlib.file_digest(archive, "sha256").hexdigest()
    if (entry["id"], entry["version"], entry["downloadUrl"], entry["sha256"]) != (
        "masterplayer", version, download_url, checksum,
    ):
        raise ValueError("The plugin manifest does not match its Release ZIP.")
    return manifest_bytes


def publish(directory, version, repository, source_sha):
    tag = f"v{version}"
    zip_name = f"PromeRadar-{tag}.zip"
    download_url = f"https://github.com/{repository}/releases/download/{tag}/{zip_name}"

    run("git", "fetch", "--no-tags", "origin", "refs/heads/main")
    run("git", "merge-base", "--is-ancestor", source_sha, "FETCH_HEAD")
    run("git", "switch", "--detach", "FETCH_HEAD")
    index = Path(MANIFEST_NAME)
    previous_bytes = index.read_bytes().replace(b"\r\n", b"\n")
    previous_entries = json.loads(previous_bytes)
    if previous_entries:
        previous, = previous_entries
        if tuple(map(int, previous["version"].split("."))) > tuple(map(int, version.split("."))):
            raise ValueError(f"The download source already points to a newer version: {previous['version']}.")

    release = release_view(repository, tag)
    tagged_sha = tag_target(tag)
    if tagged_sha is not None and tagged_sha != source_sha:
        raise ValueError(f"Tag {tag} already points to {tagged_sha}, expected {source_sha}.")

    if release is not None and not release["isDraft"]:
        if tagged_sha != source_sha:
            raise ValueError(f"Published Release {tag} has no matching tag.")
        # Reuse the published bytes if a previous run failed after publishing the Release.
        with tempfile.TemporaryDirectory(dir=directory) as temporary:
            run("gh", "release", "download", tag, "--repo", repository, "--dir", temporary,
                "--pattern", zip_name, "--pattern", MANIFEST_NAME)
            manifest_bytes = verify_assets(Path(temporary), zip_name, version, download_url)
    else:
        manifest_bytes = verify_assets(directory, zip_name, version, download_url)
        if previous_entries and previous["version"] == version:
            raise ValueError(f"The download source already points to {version}. Use a higher version.")
        if release is None:
            run("gh", "release", "create", tag, "--repo", repository, "--target", source_sha,
                "--title", f"PromeRadar {tag}", "--generate-notes", "--draft")
        elif tagged_sha is None and release["targetCommitish"] != source_sha:
            raise ValueError(f"Draft Release {tag} targets a different commit.")
        run("gh", "release", "upload", tag, str(directory / zip_name), str(directory / MANIFEST_NAME),
            "--repo", repository, "--clobber")
        run("gh", "release", "edit", tag, "--repo", repository, "--draft=false", "--latest")

    if manifest_bytes == previous_bytes:
        return
    index.write_bytes(manifest_bytes)
    run("git", "add", "--", MANIFEST_NAME)
    run("git", "-c", "user.name=github-actions[bot]", "-c",
        "user.email=41898282+github-actions[bot]@users.noreply.github.com",
        "commit", "-m", f"Update PromeRadar download source to {tag}")
    run("git", "push", "origin", "HEAD:refs/heads/main")


if __name__ == "__main__":
    publish(Path(sys.argv[1]).resolve(), sys.argv[2], os.environ["GITHUB_REPOSITORY"], os.environ["GITHUB_SHA"])
