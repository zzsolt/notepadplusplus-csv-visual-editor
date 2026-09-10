"""The import path must never disguise incomplete or related-language drafts."""
from pathlib import Path
import hashlib
import json
import sys
import tempfile
from unittest import TestCase, main

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'tools/localization'))
import import_catalogs as importer


class CatalogImportTests(TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.resource = self.root / 'resource'
        (self.resource / 'Catalogs').mkdir(parents=True)
        self.inputs = self.root / 'inputs'
        self.inputs.mkdir()
        self.curated = self.root / 'curated'
        self.curated.mkdir()
        self.source = {'Common.Cancel': {'text': 'Cancel', 'context': 'Button'},
                       'Status.Rows': {'text': 'Rows: {0:N0}', 'context': 'Count'}}
        for entry in self.source.values():
            entry['sourceHash'] = hashlib.sha256(entry['text'].encode()).hexdigest()
        self.write(self.resource / 'Catalogs/en.json', {'language': 'en', 'messages': self.source})
        self.inventory('hu')
        self.draft('hu', ['Megse', 'Sorok: {0:N0}'])

    def write(self, path, value):
        path.write_text(json.dumps(value, ensure_ascii=True), encoding='utf-8')

    def inventory(self, *codes):
        self.write(self.resource / 'languages.json', {'languages': [
            {'code': code, 'file': code + '.xml'} for code in ('en', *codes)]})

    def draft(self, code, values):
        messages = {key: {'text': text, 'sourceHash': self.source[key]['sourceHash']}
                    for key, text in zip(self.source, values)}
        self.write(self.inputs / (code + '.json'), {'language': code, 'messages': messages})
        return messages

    def assemble(self, **kwargs):
        return importer.assemble(self.resource, self.inputs, **kwargs)

    def legacy(self, content):
        (self.curated / 'hu-1.tsv').write_text(content, encoding='utf-8')
        return self.assemble(curated=self.curated,
                             expected_fingerprint=importer.fingerprint(self.source))

    def test_complete_catalogs_have_canonical_keys_and_no_extra_metadata(self):
        catalogs, report = self.assemble()
        self.assertTrue(report['complete'])
        self.assertEqual(2, report['availableCatalogs'])
        self.assertEqual(list(self.source), list(catalogs['hu']['messages']))
        self.assertEqual({'text', 'sourceHash'}, set(catalogs['hu']['messages']['Common.Cancel']))

    def test_missing_language_is_reported_and_writes_nothing(self):
        self.inventory('hu', 'fr')
        catalogs, report = self.assemble()
        self.assertEqual(['fr'], report['missing'])
        output = self.root / 'result'
        with self.assertRaisesRegex(ValueError, 'Incomplete catalog'):
            importer.write_output(output, catalogs, report, resource=self.resource)
        self.assertFalse(output.exists())

    def test_partial_staging_is_explicit_and_report_stays_incomplete(self):
        self.inventory('hu', 'fr')
        catalogs, report = self.assemble()
        output = self.root / 'stage'
        importer.write_output(output, catalogs, report, stage=True, resource=self.resource)
        self.assertFalse(report['complete'])
        self.assertEqual(['en.json', 'hu.json'], sorted(path.name for path in output.iterdir()))

    def test_partial_staging_cannot_target_canonical_directory(self):
        catalogs, report = self.assemble()
        for destination in (self.resource / 'Catalogs', self.resource / 'Catalogs/stage'):
            with self.assertRaisesRegex(ValueError, 'Partial staging cannot'):
                importer.write_output(destination, catalogs, report, stage=True, resource=self.resource)

    def test_existing_output_is_not_merged_or_deleted(self):
        catalogs, report = self.assemble()
        output = self.root / 'already-reviewed'
        output.mkdir()
        marker = output / 'keep.txt'
        marker.write_text('keep')
        with self.assertRaisesRegex(ValueError, 'already exists'):
            importer.write_output(output, catalogs, report, resource=self.resource)
        self.assertEqual('keep', marker.read_text())

    def test_catalog_identity_is_not_inferred_from_filename(self):
        path = self.inputs / 'hu.json'
        draft = json.loads(path.read_text())
        draft['language'] = 'de'
        self.write(path, draft)
        catalogs, report = self.assemble()
        self.assertNotIn('hu', catalogs)
        self.assertIn('identity', report['invalid']['hu'])

    def test_duplicate_json_keys_are_rejected(self):
        (self.inputs / 'hu.json').write_text('{"language":"hu","language":"en"}')
        _, report = self.assemble()
        self.assertIn('Duplicate JSON key', report['invalid']['hu'])

    def test_stale_translation_hash_is_not_regenerated(self):
        path = self.inputs / 'hu.json'
        draft = json.loads(path.read_text())
        draft['messages']['Common.Cancel']['sourceHash'] = '0' * 64
        self.write(path, draft)
        _, report = self.assemble()
        self.assertIn('stale source hash', report['invalid']['hu'])

    def test_missing_and_extra_keys_are_invalid(self):
        for extra in (False, True):
            draft = self.draft('hu', ['Megse', 'Sorok: {0:N0}'])
            if extra:
                draft['Unknown'] = draft['Common.Cancel']
            else:
                del draft['Common.Cancel']
            self.write(self.inputs / 'hu.json', {'language': 'hu', 'messages': draft})
            _, report = self.assemble()
            self.assertIn('key coverage', report['invalid']['hu'])

    def test_argument_format_and_multiplicity_are_preserved(self):
        self.draft('hu', ['Megse', 'Sorok: {0:N0}'])
        self.draft('hu', ['Megse', 'Sorok: {0}'])
        _, report = self.assemble()
        self.assertIn('format arguments', report['invalid']['hu'])
        self.draft('hu', ['Megse', 'Sorok: {0:N0} {0:N0}'])
        _, report = self.assemble()
        self.assertIn('format arguments', report['invalid']['hu'])

    def test_provider_alias_uses_same_language_and_canonical_identity(self):
        self.inventory('nb')
        self.draft('no', ['Avbryt', 'Rader: {0:N0}'])
        catalogs, report = self.assemble()
        self.assertTrue(report['complete'])
        self.assertEqual('nb', catalogs['nb']['language'])

    def test_related_languages_and_scripts_are_not_silent_aliases(self):
        self.inventory('yue-Hant', 'oc-aran', 'sr-Latn', 'uz-Cyrl', 'pt-BR', 'es-AR')
        for code in ('zh_Hant', 'oc', 'sr', 'uz', 'pt', 'es'):
            self.draft(code, ['Other', 'Other: {0:N0}'])
        catalogs, report = self.assemble()
        self.assertEqual({'en'}, set(catalogs))
        self.assertEqual(6, len(report['missing']))

    def test_legacy_import_requires_exact_order_fingerprint(self):
        (self.curated / 'hu-1.tsv').write_text('000\tMegse\n001\tSorok: {0:N0}\n')
        for value in (None, '0' * 64):
            _, report = self.assemble(curated=self.curated, expected_fingerprint=value)
            self.assertIn('fingerprint', report['invalid']['hu'])
        reordered = dict(reversed(list(self.source.items())))
        self.assertNotEqual(importer.fingerprint(self.source), importer.fingerprint(reordered))

    def test_legacy_unicode_and_line_breaks_survive_without_mojibake(self):
        catalogs, report = self.legacy('000\tM\u00e9gse\n001\tSorok:\\n{0:N0}\n')
        self.assertTrue(report['complete'])
        self.assertEqual('M\u00e9gse', catalogs['hu']['messages']['Common.Cancel']['text'])
        self.assertEqual('Sorok:\n{0:N0}', catalogs['hu']['messages']['Status.Rows']['text'])

    def test_legacy_duplicate_and_missing_indices_are_not_accepted(self):
        for text in ('000\tMegse\n000\tMegse\n', '000\tMegse\n',
                     '003\tMegse\n', 'wrong\tMegse\n'):
            catalogs, report = self.legacy(text)
            self.assertNotIn('hu', catalogs)
            self.assertIn('hu', report['invalid'])

    def test_unrecognized_escapes_are_not_silently_changed(self):
        with self.assertRaisesRegex(ValueError, 'Unsupported escape'):
            importer.decode_tsv_text(r'\u0020')
        self.assertEqual('\\n', importer.decode_tsv_text(r'\\n'))

    def test_empty_and_surrogate_text_are_invalid(self):
        for text in ('  ', '\ud800'):
            self.draft('hu', [text, 'Sorok: {0:N0}'])
            catalogs, report = self.assemble()
            self.assertNotIn('hu', catalogs)
            self.assertIn('hu', report['invalid'])

    def test_mixed_han_text_in_abkhaz_draft_is_rejected(self):
        self.inventory('ab')
        self.draft('ab', ['\u0410\u8054\u7cfb', '\u0410: {0:N0}'])
        _, report = self.assemble()
        self.assertIn('unexpected Han', report['invalid']['ab'])

    def test_bad_english_hash_blocks_import(self):
        self.source['Common.Cancel']['text'] = 'Changed source'
        self.write(self.resource / 'Catalogs/en.json', {'language': 'en', 'messages': self.source})
        with self.assertRaisesRegex(ValueError, 'Stale English'):
            self.assemble()

    def test_keyed_override_corrects_text_without_changing_other_entries(self):
        fixes = self.root / 'fixes.json'
        replacement = {'text': 'M\u00e9gse', 'sourceHash': self.source['Common.Cancel']['sourceHash']}
        self.write(fixes, {'hu': {'Common.Cancel': replacement}})
        catalogs, report = self.assemble(overrides_path=fixes)
        self.assertTrue(report['complete'])
        self.assertEqual(replacement, catalogs['hu']['messages']['Common.Cancel'])
        self.assertEqual('Sorok: {0:N0}', catalogs['hu']['messages']['Status.Rows']['text'])

    def test_partial_override_does_not_create_a_fake_complete_language(self):
        self.inventory('hu', 'fr')
        fixes = self.root / 'fixes.json'
        self.write(fixes, {'fr': {'Common.Cancel': {
            'text': 'Annuler', 'sourceHash': self.source['Common.Cancel']['sourceHash']}}})
        catalogs, report = self.assemble(overrides_path=fixes)
        self.assertNotIn('fr', catalogs)
        self.assertEqual(['fr'], report['missing'])

    def test_override_cannot_refresh_stale_hashes_or_add_unknown_keys(self):
        fixes = self.root / 'fixes.json'
        for value in ({'hu': {'Common.Cancel': {'text': 'Megse', 'sourceHash': '0' * 64}}},
                      {'hu': {'Unknown': {'text': 'Megse'}}}, {'unknown': {}}, {'en': {}}):
            self.write(fixes, value)
            with self.assertRaises(ValueError):
                self.assemble(overrides_path=fixes)

    def test_output_is_utf8_json_with_final_newline(self):
        self.draft('hu', ['M\u00e9gse', 'Sorok: {0:N0}'])
        catalogs, report = self.assemble()
        output = self.root / 'result'
        importer.write_output(output, catalogs, report, resource=self.resource)
        raw = (output / 'hu.json').read_bytes()
        self.assertIn('M\u00e9gse'.encode(), raw)
        self.assertTrue(raw.endswith(b'\n'))
        self.assertEqual(catalogs['hu'], json.loads(raw))


if __name__ == '__main__':
    main()
