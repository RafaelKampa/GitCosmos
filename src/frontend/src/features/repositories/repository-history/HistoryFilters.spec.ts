import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { mount, flushPromises, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import HistoryFilters from './HistoryFilters.vue';
import { RepositoryBranchApiService } from '../../../core/repositories/repository-branch-api.service';
import { ApiError } from '../../../core/api/api-error';
import { useLanguageStore } from '../../../core/i18n/language.store';
import type { LocalGitBranches } from '../../../core/repositories/models/repository-branch.model';
import type { CustomResponse } from '../../../shared/models/custom-response.model';
const data: LocalGitBranches = { currentBranch: 'main', isDetached: false, hasCommits: true, branches: [{ name: 'main', sha: 'a'.repeat(40), upstream: null, isCurrent: true, isCheckedOut: true }] };
const ok = (result: LocalGitBranches | null): CustomResponse<LocalGitBranches> => ({ success: true, result, errors: [], messages: [] });
let view: VueWrapper | undefined;
beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); });
afterEach(() => { view?.unmount(); view = undefined; vi.unstubAllGlobals(); });
function render() { view = mount(HistoryFilters, { props: { repositoryId: 2, workspaceId: 1, branch: 'feature/new', search: 'old' } }); return view; }
it('loads branch choices on focus using the actual HTTP service and emits literal trimmed filters', async () => {
  const fetchMock = vi.fn(async () => new Response(JSON.stringify(ok(data)))); vi.stubGlobal('fetch', fetchMock);
  useLanguageStore().setLanguage('es'); const page = render(); expect(fetchMock).not.toHaveBeenCalled();
  expect(page.findAll('option').map(option => option.element.value)).toEqual(['', 'feature/new']);
  await page.get('select').trigger('focus'); await flushPromises();
  expect(fetchMock).toHaveBeenCalledWith('/api/RepositoryBranch/GetBranches?repositoryId=2&workspaceId=1', expect.objectContaining({ headers: { 'Accept-Language': 'es' } }));
  await page.get('select').setValue('main'); await page.get('input').setValue('  [literal] abcd  '); await page.get('form').trigger('submit');
  expect(page.emitted('apply')![0]).toEqual([{ branch: 'main', search: '[literal] abcd' }]);
  await page.setProps({ branch: '', search: '' }); expect(page.get<HTMLSelectElement>('select').element.value).toBe(''); expect(page.get<HTMLInputElement>('input').element.value).toBe('');
  useLanguageStore().setLanguage('en'); await flushPromises(); expect(page.findAll('option')).toHaveLength(1);
  await page.get('select').trigger('focus'); await flushPromises(); expect(fetchMock).toHaveBeenLastCalledWith(expect.any(String), expect.objectContaining({ headers: { 'Accept-Language': 'en' } }));
});
it.each([new ApiError('Localized'), new Error('private'), null])('shows failed branch lookups and retries without applying filters: %s', async error => {
  const api = vi.spyOn(RepositoryBranchApiService.prototype, 'getBranches').mockResolvedValue(ok(data));
  if (error) api.mockRejectedValueOnce(error); else api.mockResolvedValueOnce(ok(null));
  const page = render(); await page.get('select').trigger('focus'); await flushPromises();
  expect(page.get('[role="alert"]').text()).not.toContain('private');
  await page.get('.history-branches-retry').trigger('click'); await flushPromises(); expect(page.find('[role="alert"]').exists()).toBe(false); expect(page.emitted('apply')).toBeUndefined();
});
it.each([false, true])('cancels pending lookup on scope change/unmount and ignores late response (error=%s)', async failure => {
  let resolve!: (value: CustomResponse<LocalGitBranches>) => void; let reject!: (error: Error) => void;
  const pending = new Promise<CustomResponse<LocalGitBranches>>((ok, fail) => { resolve = ok; reject = fail; });
  const api = vi.spyOn(RepositoryBranchApiService.prototype, 'getBranches').mockReturnValueOnce(pending).mockResolvedValue(ok(data));
  const page = render(); await page.get('select').trigger('focus'); await page.get('select').trigger('focus'); expect(api).toHaveBeenCalledTimes(1);
  await page.setProps({ repositoryId: 3, workspaceId: 0 }); expect(api.mock.calls[0]![2]!.aborted).toBe(true);
  if (failure) reject(new Error('late')); else resolve(ok(data)); await flushPromises(); expect(page.findAll('option')).toHaveLength(2);
  await page.get('select').trigger('focus'); await flushPromises(); page.unmount(); view = undefined; expect(api.mock.calls[1]![2]!.aborted).toBe(true);
});
