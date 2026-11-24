import { expect, it } from 'vitest';
import { parseCommitReferences, formatCommitDate } from './history-presentation';

it('identifies current local branches, remote branches, tags and detached HEAD with short labels', () => {
  expect(parseCommitReferences('  ')).toEqual([]);
  expect(parseCommitReferences('HEAD -> refs/heads/main, refs/heads/feature/a,b, refs/remotes/origin/main, tag: refs/tags/v1, HEAD, refs/notes/commits')).toEqual([
    { label: 'main', fullName: 'HEAD -> refs/heads/main', kind: 'branch', current: true },
    { label: 'feature/a,b', fullName: 'refs/heads/feature/a,b', kind: 'branch', current: false },
    { label: 'origin/main', fullName: 'refs/remotes/origin/main', kind: 'remote', current: false },
    { label: 'v1', fullName: 'tag: refs/tags/v1', kind: 'tag', current: false },
    { label: 'HEAD', fullName: 'HEAD', kind: 'head', current: true },
    { label: 'refs/notes/commits', fullName: 'refs/notes/commits', kind: 'head', current: false }
  ]);
});

it.each(['pt-BR', 'es', 'en'])('formats a human-readable date and time in %s while preserving invalid input', language => {
  const formatted = formatCommitDate('2026-11-19T11:28:00-03:00', language);
  expect(formatted).toContain('2026'); expect(formatted).toMatch(/\d{2}:\d{2}/u);
  expect(formatted).not.toContain('T11:28');
  expect(formatCommitDate('invalid', language)).toBe('invalid');
});
