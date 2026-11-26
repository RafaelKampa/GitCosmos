import { afterEach, expect, it, vi } from 'vitest';
import { CommitExplorerApiService } from './commit-explorer-api.service';
afterEach(() => vi.unstubAllGlobals());
it('sends scoped immutable references, literal paths and explicit native/restore actions', async () => {
  const fetch = vi.fn().mockImplementation(async () => new Response(JSON.stringify({ success: true, result: [], errors: [], messages: [] })));
  vi.stubGlobal('fetch', fetch); const api = new CommitExplorerApiService(() => 'es', () => 'error');
  const file = { repositoryId: 2, workspaceId: 1, sha: 'a'.repeat(40), path: 'folder/a & b.cs' };
  for (const operation of ['tree', 'file', 'history', 'blame', 'tools'] as const) { await api[operation](file); const [url, options] = fetch.mock.calls.at(-1)!; expect(new URL(url, 'http://localhost').searchParams.get('path')).toBe(file.path); expect(options.headers['Accept-Language']).toBe('es'); }
  await api.prepare(file, true); expect(JSON.parse(fetch.mock.calls.at(-1)![1].body)).toEqual({ file, before: true });
  await api.restore(file, false, 'token'); expect(JSON.parse(fetch.mock.calls.at(-1)![1].body)).toEqual({ file, before: false, token: 'token' });
  await api.desktop(file, 'diff', 'vscode'); expect(JSON.parse(fetch.mock.calls.at(-1)![1].body)).toEqual({ file, action: 'diff', tool: 'vscode' });
});
