#!/usr/bin/env python3
"""Compile actual pure-core sources and run all their NUnit cases. Not Unity acceptance."""
import argparse
import datetime as dt
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]


def source_records():
    records = []
    for folder in ('Assets/UndeadOvertime/Core', 'Assets/UndeadOvertime/Tests/CoreEditMode'):
        files = sorted((REPO / folder).glob('*.cs'))
        if not files:
            raise RuntimeError('No C# sources found in ' + folder)
        for path in files:
            data = path.read_bytes()
            records.append({'path': path.relative_to(REPO).as_posix(), 'bytes': len(data),
                            'sha256': hashlib.sha256(data).hexdigest(),
                            'git_blob_sha1': hashlib.sha1(b'blob ' + str(len(data)).encode() + b'\0' + data).hexdigest()})
    return records


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet', help='Path to an already installed, approved .NET SDK executable.')
    args = parser.parse_args()
    dotnet = shutil.which(args.dotnet or 'dotnet')
    if not dotnet:
        raise RuntimeError('No .NET SDK found. Install the pinned official SDK before running; this script does not install it.')
    dotnet = str(Path(dotnet).resolve())
    pinned = json.loads((HERE / 'global.json').read_text())['sdk']['version']
    stamp = dt.datetime.now(dt.timezone.utc).strftime('%Y%m%dT%H%M%S%fZ')
    output = HERE / 'results' / stamp
    output.mkdir(parents=True, exist_ok=False)
    cache = HERE / '.cache'
    env = os.environ.copy()
    # Keep first-run files, packages and Unix runtime state in this scoped harness directory.
    for key, child in (('DOTNET_CLI_HOME', 'home'), ('NUGET_PACKAGES', 'packages'),
                       ('XDG_RUNTIME_DIR', 'runtime'), ('XDG_DATA_HOME', 'data'), ('XDG_CACHE_HOME', 'cache')):
        location = cache / child
        location.mkdir(parents=True, exist_ok=True)
        env[key] = str(location)
    env.update(DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_NOLOGO='1',
               DOTNET_GENERATE_ASPNET_CERTIFICATE='false', DOTNET_CLI_DO_NOT_USE_MSBUILD_SERVER='1')
    # An explicitly selected executable owns runtime resolution; do not mutate machine PATH or settings.
    env['DOTNET_ROOT'] = str(Path(dotnet).parent)
    before = source_records()
    locks = [HERE / 'Core/packages.lock.json', HERE / 'Runner/packages.lock.json']
    lock_before = {p.relative_to(HERE).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in locks}
    (output / 'sources-before.json').write_text(json.dumps(before, indent=2) + '\n')

    def run(name, command):
        result = subprocess.run(command, cwd=HERE, env=env, stdout=subprocess.PIPE,
                                stderr=subprocess.STDOUT, text=True, check=False)
        (output / (name + '.log')).write_text(result.stdout, encoding='utf-8')
        print(result.stdout, end='', flush=True)
        return result

    version = run('sdk-version', [dotnet, '--version'])
    if version.returncode or version.stdout.strip() != pinned:
        raise RuntimeError('Pinned SDK ' + pinned + ' is required; see ' + str(output))
    run('sdk-info', [dotnet, '--info'])
    project = 'Runner/UndeadOvertime.Core.CloudTests.csproj'
    for name, command in (
        ('restore', [dotnet, 'restore', project, '--locked-mode', '--configfile', 'NuGet.Config', '--verbosity', 'minimal']),
        ('build', [dotnet, 'build', project, '--no-restore', '--configuration', 'Release', '--verbosity', 'minimal',
                   '--disable-build-servers', '-p:UseSharedCompilation=false'])):
        result = run(name, command)
        if result.returncode:
            raise RuntimeError(name + ' failed; see ' + str(output))
    xml_path = output / 'nunit-results.xml'
    result = run('tests', [dotnet, 'Runner/bin/Release/net10.0/UndeadOvertime.Core.CloudTests.dll',
                          '--workers=0', '--seed=103403', '--result=' + str(xml_path) + ';format=nunit3'])
    if not xml_path.is_file():
        raise RuntimeError('NUnit produced no result XML; see ' + str(output))
    xml = ET.parse(xml_path).getroot()
    after = source_records()
    (output / 'sources-after.json').write_text(json.dumps(after, indent=2) + '\n')
    lock_after = {p.relative_to(HERE).as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in locks}
    counts = {name: int(xml.get(name, '0')) for name in ('total', 'passed', 'failed', 'warnings', 'inconclusive', 'skipped')}
    unchanged = before == after and lock_before == lock_after
    passed = (result.returncode == 0 and xml.get('result') == 'Passed' and counts['total'] > 0
              and counts['total'] == counts['passed'] and all(counts[x] == 0 for x in ('failed', 'warnings', 'inconclusive', 'skipped'))
              and unchanged)
    summary = {'schema_version': 1, 'scope': 'Pure C# core only; not Unity, physics, materials, rendering, player build or LAN acceptance.',
               'success': passed, 'sdk_version': pinned, 'core_target': 'netstandard2.1', 'runner_target': 'net10.0',
               'language_version': '9.0', 'nunit_version': '3.14.0', 'nunitlite_version': '3.14.0',
               'test_exit_code': result.returncode, 'result': xml.get('result'), 'counts': counts,
               'start_time_utc': xml.get('start-time'), 'end_time_utc': xml.get('end-time'), 'duration_seconds': xml.get('duration'),
               'sources_and_locks_unchanged': unchanged, 'sources': after, 'lockfile_sha256': lock_after,
               'fixtures': [{'name': x.get('fullname'), 'result': x.get('result'), 'total': int(x.get('total', '0')),
                             'passed': int(x.get('passed', '0')), 'failed': int(x.get('failed', '0'))}
                            for x in xml.iter('test-suite') if x.get('type') == 'TestFixture'],
               'nunit_xml_sha256': hashlib.sha256(xml_path.read_bytes()).hexdigest()}
    (output / 'summary.json').write_text(json.dumps(summary, indent=2) + '\n')
    print('Core verification ' + ('PASS' if passed else 'FAIL') + ': ' + str(output), flush=True)
    return 0 if passed else 1


if __name__ == '__main__':
    try:
        sys.exit(main())
    except (OSError, RuntimeError, ValueError, ET.ParseError) as error:
        print('Core verification FAILED: ' + str(error), file=sys.stderr)
        sys.exit(1)
