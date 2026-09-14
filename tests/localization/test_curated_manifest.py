from pathlib import Path
import hashlib
import json
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'tools/localization'))
import import_catalogs as importer


class CuratedManifestTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.resource = self.root / 'resource'
        (self.resource / 'Catalogs').mkdir(parents=True)
        self.inputs = self.root / 'inputs'; self.inputs.mkdir()
        self.curated = self.root / 'curated'; self.curated.mkdir()
        self.source = {'Common.Cancel': {'text': 'Cancel'}, 'Status.Rows': {'text': 'Rows: {0:N0}'}}
        for value in self.source.values():
            value['sourceHash'] = hashlib.sha256(value['text'].encode()).hexdigest()
        self.write(self.resource / 'Catalogs/en.json', {'language': 'en', 'messages': self.source})
        self.write(self.resource / 'languages.json', {'languages': [{'code': 'en'}, {'code': 'hu'}]})

    def write(self, path, value):
        path.write_text(json.dumps(value), encoding='utf-8')

    def assemble(self, **kwargs):
        return importer.assemble(self.resource, self.inputs, **kwargs)

    def test_curated_manifest_rejects_recomputed_fingerprint_after_english_changes(self):
        path = self.curated / 'hu-1.tsv'
        path.write_text('000\tMegse\n001\tSorok: {0:N0}\n', encoding='utf-8')
        self.write(self.curated / 'manifest.json', {
            'format': 1, 'sourceFingerprint': importer.fingerprint(self.source),
            'keyCount': len(self.source), 'files': {path.name: hashlib.sha256(path.read_bytes()).hexdigest()}})
        self.source['Common.Cancel']['text'] = 'New caption'
        self.source['Common.Cancel']['sourceHash'] = hashlib.sha256(b'New caption').hexdigest()
        self.write(self.resource / 'Catalogs/en.json', {'language': 'en', 'messages': self.source})
        _, report = self.assemble(curated=self.curated,
                                  expected_fingerprint=importer.fingerprint(self.source))
        self.assertIn('manifest', report['invalid']['hu'])

    def test_curated_manifest_rejects_changed_legacy_bytes(self):
        path = self.curated / 'hu-1.tsv'
        path.write_text('000\tMegse\n001\tSorok: {0:N0}\n', encoding='utf-8')
        self.write(self.curated / 'manifest.json', {
            'format': 1, 'sourceFingerprint': importer.fingerprint(self.source),
            'keyCount': len(self.source), 'files': {path.name: '0' * 64}})
        _, report = self.assemble(curated=self.curated,
                                  expected_fingerprint=importer.fingerprint(self.source))
        self.assertIn('reviewed manifest', report['invalid']['hu'])

    def test_curated_manifest_accepts_exact_source_and_bytes(self):
        path = self.curated / 'hu-1.tsv'
        path.write_text('000\tMegse\n001\tSorok: {0:N0}\n', encoding='utf-8')
        self.write(self.curated / 'manifest.json', {
            'format': 1, 'sourceFingerprint': importer.fingerprint(self.source),
            'keyCount': len(self.source), 'files': {path.name: hashlib.sha256(path.read_bytes()).hexdigest()}})
        _, report = self.assemble(curated=self.curated,
                                  expected_fingerprint=importer.fingerprint(self.source))
        self.assertTrue(report['complete'])


if __name__ == '__main__':
    unittest.main()
