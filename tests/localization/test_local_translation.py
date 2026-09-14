from pathlib import Path
import hashlib
import json
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[2] / 'tools/localization'))
import translate_local as local


def entry(text):
    return {'text': text, 'sourceHash': hashlib.sha256(text.encode()).hexdigest()}


class FakeEngine:
    def __init__(self):
        self.calls = []

    def supports(self, language):
        return language in ('hu', 'de', 'no')

    def translate(self, language, texts):
        self.calls.append((language, texts))
        return ['Translated ' + text for text in texts]


class LocalTranslationTests(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.addCleanup(self.tmp.cleanup)
        self.root = Path(self.tmp.name)
        self.resource = self.root / 'resources'
        (self.resource / 'Catalogs').mkdir(parents=True)
        self.output = self.root / 'drafts'
        self.source = {'A': entry('Rows {0:N0} and {0:N0}'), 'B': entry('Copy CSV')}
        self.write(self.resource / 'Catalogs/en.json', {'language': 'en', 'messages': self.source})
        self.inventory('hu', 'de')

    def write(self, path, value):
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(json.dumps(value), encoding='utf-8')

    def inventory(self, *codes):
        self.write(self.resource / 'languages.json', {'languages': [{'code': code} for code in ('en', *codes)]})

    def test_token_dictionary_keys_not_values_are_counted(self):
        text, tokens = local.protect('Rows {0:N0} / {0:N0}, CSV -12.5')
        self.assertEqual('Rows {0:N0} / {0:N0}, CSV -12.5', local.restore(text, tokens))
        self.assertEqual(4, len(tokens))

    def test_missing_duplicate_or_extra_marker_is_rejected(self):
        text, tokens = local.protect('Rows {0:N0}')
        for broken in ('Rows', text + ' {0}', text + ' {99}'):
            with self.assertRaises(ValueError): local.restore(broken, tokens)

    def test_literal_escaped_braces_survive(self):
        value = '{{sample}} {0:N0}'
        prepared, tokens = local.protect(value)
        self.assertEqual(value, local.restore(prepared, tokens))

    def test_model_loaded_once_for_multiple_languages(self):
        loaded = []
        engine = FakeEngine()
        def factory(): loaded.append(True); return engine
        report = local.process(self.resource, self.output, factory)
        self.assertEqual([True], loaded)
        self.assertEqual({'hu', 'de'}, {code for code, _ in engine.calls})
        self.assertTrue(report['complete'])

    def test_plan_does_not_create_outputs_or_load_dependencies(self):
        report = local.process(self.resource, self.output, None)
        self.assertFalse(self.output.exists())
        self.assertEqual(1, report['availableCatalogs'])
        self.assertFalse(report['complete'])

    def test_resume_does_not_regenerate_current_entries(self):
        engine = FakeEngine()
        local.process(self.resource, self.output, lambda: engine)
        def forbidden(): raise AssertionError('No pending entries; model must not be loaded')
        self.assertTrue(local.process(self.resource, self.output, forbidden, resume=True)['complete'])

    def test_only_changed_key_is_translated(self):
        engine = FakeEngine()
        local.process(self.resource, self.output, lambda: engine)
        self.source['A'] = entry('Changed rows {0:N0}')
        self.write(self.resource / 'Catalogs/en.json', {'language': 'en', 'messages': self.source})
        engine.calls.clear()
        report = local.process(self.resource, self.output, lambda: engine, resume=True)
        self.assertTrue(report['complete'])
        self.assertTrue(all(detail['requested'] == 1 for detail in report['languages'].values()))
        self.assertTrue(all(len(texts) == 1 for _, texts in engine.calls))

    def test_shipped_catalogs_never_overwritten(self):
        for output in (self.resource / 'Catalogs', self.resource / 'Catalogs/stage'):
            with self.assertRaises(ValueError): local.process(self.resource, output, lambda: FakeEngine())

    def test_existing_output_requires_explicit_resume(self):
        self.output.mkdir()
        with self.assertRaisesRegex(ValueError, '--resume'):
            local.process(self.resource, self.output, lambda: FakeEngine())

    def test_failed_sentence_is_not_translated_as_fragments(self):
        engine = FakeEngine()
        engine.translate = lambda language, texts: ['broken' for _ in texts]
        messages, report = local.translate_messages('hu', self.source, {}, engine)
        self.assertEqual({}, messages)
        self.assertEqual(2, len(report['invalid']))

    def test_failed_stale_entry_does_not_receive_fresh_hash(self):
        engine = FakeEngine()
        engine.translate = lambda language, texts: ['broken' for _ in texts]
        old = {'A': entry('Old rows {0}')}
        messages, _ = local.translate_messages('hu', self.source, old, engine)
        self.assertEqual(old['A'], messages['A'])
        self.assertNotEqual(self.source['A']['sourceHash'], messages['A']['sourceHash'])

    def test_related_languages_are_not_silent_aliases(self):
        engine = FakeEngine()
        for code in ('oc-aran', 'yue-Hant', 'pt-BR', 'pt-PT', 'sr-Cyrl', 'uz-Latn'):
            messages, report = local.translate_messages(code, self.source, {}, engine)
            self.assertFalse(messages)
            self.assertIn('language', report['invalid'])
        self.assertFalse(engine.calls)

    def test_allowed_same_language_provider_alias(self):
        engine = FakeEngine()
        _, report = local.translate_messages('nb', self.source, {}, engine)
        self.assertFalse(report['invalid'])
        self.assertEqual('no', engine.calls[0][0])

    def test_requested_subset_does_not_claim_global_completion(self):
        report = local.process(self.resource, self.output, lambda: FakeEngine(), requested=['hu'])
        self.assertFalse(report['complete'])
        self.assertEqual(['de'], report['missingCatalogs'])

    def test_bad_source_hash_rejected_before_model_load(self):
        self.source['A']['sourceHash'] = '0' * 64
        self.write(self.resource / 'Catalogs/en.json', {'language': 'en', 'messages': self.source})
        with self.assertRaisesRegex(ValueError, 'Stale English'):
            local.process(self.resource, self.output, lambda: FakeEngine())

    def test_unknown_language_rejected(self):
        with self.assertRaisesRegex(ValueError, 'known non-English'):
            local.process(self.resource, self.output, lambda: FakeEngine(), requested=['unknown'])

    def test_unknown_keys_do_not_disappear_silently(self):
        self.write(self.resource / 'Catalogs/hu.json', {'language': 'hu', 'messages': {'bad': entry('Unknown')}})
        with self.assertRaisesRegex(ValueError, 'Unknown message keys'):
            local.process(self.resource, self.output, None)

    def test_draft_identity_is_checked(self):
        self.write(self.output / 'hu.json', {'language': 'de', 'messages': {}})
        with self.assertRaisesRegex(ValueError, 'identity'):
            local.process(self.resource, self.output, None)

    def test_line_breaks_tabs_and_resource_whitespace_are_preserved(self):
        source = {'A': entry('  Copy CSV\r\nRows {0}\tNext  ')}
        messages, report = local.translate_messages('hu', source, {}, FakeEngine())
        self.assertFalse(report['invalid'])
        text = messages['A']['text']
        self.assertTrue(text.startswith('  '))
        self.assertTrue(text.endswith('  '))
        self.assertIn('\r\n', text)
        self.assertIn('\t', text)

    def test_duplicate_sentences_batched_only_once(self):
        engine = FakeEngine()
        local.translate_messages('hu', {'A': entry('Copy CSV'), 'B': entry('Copy CSV')}, {}, engine)
        self.assertEqual(1, len(engine.calls[0][1]))

    def test_bad_model_batch_fails_closed(self):
        engine = FakeEngine()
        engine.translate = lambda language, texts: []
        with self.assertRaisesRegex(ValueError, 'invalid batch'):
            local.translate_messages('hu', self.source, {}, engine)

    def test_offline_model_missing_is_explained_before_imports(self):
        with self.assertRaisesRegex(ValueError, 'predownloaded'):
            local.OfflineEngine(self.root / 'missing')


if __name__ == '__main__':
    unittest.main()
