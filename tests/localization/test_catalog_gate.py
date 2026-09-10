"""Regression checks for the mandatory catalog gate, using isolated synthetic input."""
from pathlib import Path
from unittest import TestCase, main
from unittest.mock import patch, MagicMock
import hashlib
import importlib.util
import json
import subprocess
import sys
import tempfile

TOOLS = Path(__file__).resolve().parents[2] / 'tools/localization'
sys.path.insert(0, str(TOOLS))
import verify


class CatalogGateTests(TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.resource = self.root / 'src/CsvVisualEditor.Localization'
        (self.resource / 'Catalogs').mkdir(parents=True)
        self.patchers = [patch.object(verify, 'ROOT', self.root), patch.object(verify, 'RESOURCE', self.resource),
                         patch.object(verify.subprocess, 'run', return_value=None)]
        for p in self.patchers:
            p.start(); self.addCleanup(p.stop)
        self.inventory = {'languages': [{'code': 'en', 'file': 'english.xml'}, {'code': 'hu', 'file': 'hungarian.xml'}]}
        self.source = {'Test.Rows': {'text': 'Rows: {0:N0}', 'context': 'Visible row count'}}
        self.target = {'Test.Rows': {'text': 'Sorok: {0:N0}'}}
        self.save()

    def write(self, path, value):
        (self.resource / path).write_text(json.dumps(value, ensure_ascii=True), encoding='utf-8')

    def save(self):
        for key, entry in self.source.items():
            entry['sourceHash'] = hashlib.sha256(entry['text'].encode()).hexdigest()
            if key in self.target: self.target[key]['sourceHash'] = entry['sourceHash']
        self.write('languages.json', self.inventory)
        self.write('Catalogs/en.json', {'language': 'en', 'messages': self.source})
        self.write('Catalogs/hu.json', {'language': 'hu', 'messages': self.target})
        self.write('raw-literals.json', {'files': {}})

    def fails(self, pattern):
        with self.assertRaisesRegex(SystemExit, pattern): verify.verify()

    def test_complete_catalog_passes(self):
        verify.verify()

    def test_missing_language_fails(self):
        (self.resource / 'Catalogs/hu.json').unlink()
        self.fails('Catalog inventory mismatch')

    def test_missing_key_fails(self):
        self.target.clear(); self.save(); self.fails('Key coverage mismatch')

    def test_stale_translation_fails(self):
        self.target['Test.Rows']['sourceHash'] = '0' * 64
        self.write('Catalogs/hu.json', {'language': 'hu', 'messages': self.target})
        self.fails('stale translation')

    def test_changed_format_fails(self):
        self.target['Test.Rows']['text'] = 'Sorok: {0}'
        self.save(); self.fails('changed placeholders')

    def test_repeated_argument_cannot_be_dropped(self):
        self.source['Test.Rows']['text'] = '{0} / {0}'
        self.target['Test.Rows']['text'] = '{0}'
        self.save(); self.fails('changed placeholders')

    def test_broken_braces_fail(self):
        self.target['Test.Rows']['text'] = 'Sorok: {0'
        self.save(); self.fails('Malformed format')

    def test_copied_english_prose_is_not_a_translation(self):
        self.source['Test.Rows']['text'] = 'The table contains {0:N0} rows. Refresh the document before applying any changes to the source buffer.'
        self.target['Test.Rows']['text'] = self.source['Test.Rows']['text']
        self.save(); self.fails('untranslated prose')

    def test_repeated_decoding_output_fails(self):
        self.target['Test.Rows']['text'] = 'TryTryTryTryTry {0:N0}'
        self.save(); self.fails('repeated translation output')

    def test_numeric_syntax_example_is_not_localized(self):
        self.source['Test.Rows']['text'] = 'Use -12.5.'
        self.target['Test.Rows']['text'] = 'Hasznald: -12,5.'
        self.save(); self.fails('altered numeric syntax example')

    def test_new_nested_ui_literal_is_detected(self):
        folder = self.root / 'src/CsvVisualEditor/Dialogs'
        folder.mkdir(parents=True)
        (folder / 'NewDialog.cs').write_text('class D { string Text = "New caption"; }')
        self.fails('Review untranslated literals in Dialogs/NewDialog.cs')

    def test_invalid_unicode_fails(self):
        self.target['Test.Rows']['text'] = '\ud800 {0:N0}'
        self.save(); self.fails('invalid Unicode')

    def test_duplicate_keys_fail(self):
        (self.resource / 'Catalogs/hu.json').write_text('{"language":"hu","language":"en"}')
        with self.assertRaisesRegex(ValueError, 'Duplicate JSON key'): verify.verify()

    def test_new_host_language_requires_catalog(self):
        self.inventory['languages'].append({'file': 'french.xml', 'code': 'fr'})
        self.save(); self.fails('Catalog inventory mismatch')

    def test_upstream_inventory_difference_is_a_failure(self):
        response = MagicMock()
        response.__enter__.return_value.read.return_value = b'{L"English", L"english.xml"}, {L"Magyar", L"hungarian.xml"}, {L"French", L"french.xml"}'
        with patch.object(verify.urllib.request, 'urlopen', return_value=response):
            with self.assertRaisesRegex(SystemExit, 'Official language inventory changed'): verify.verify(check_upstream=True)

    def test_abkhaz_catalog_rejects_unexpected_han_text(self):
        self.inventory['languages'].append({'file': 'abkhazian.xml', 'code': 'ab'})
        self.save()
        self.write('Catalogs/ab.json', {'language': 'ab', 'messages': {
            'Test.Rows': {'text': '\u0410\u8054\u7cfb {0:N0}',
                          'sourceHash': self.source['Test.Rows']['sourceHash']}}})
        self.fails('unexpected Han text in Abkhaz catalog')

    def test_generated_code_failure_is_not_ignored(self):
        with patch.object(verify.subprocess, 'run', side_effect=subprocess.CalledProcessError(1, 'generate')):
            with self.assertRaises(subprocess.CalledProcessError): verify.verify()


if __name__ == '__main__': main()
