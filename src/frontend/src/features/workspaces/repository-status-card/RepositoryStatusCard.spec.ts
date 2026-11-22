import { afterEach, beforeEach, expect, it } from 'vitest';
import { mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import RepositoryStatusCard from './RepositoryStatusCard.vue';
import { useLanguageStore } from '../../../core/i18n/language.store';
import type { RepositoryDashboardResponse } from '../../../core/workspaces/models/responses/workspace-dashboard-response.model';

let wrapper: VueWrapper | undefined;
beforeEach(() => { localStorage.clear(); setActivePinia(createPinia()); });
afterEach(() => wrapper?.unmount());
const repository: RepositoryDashboardResponse = { id: 1, name: 'Repo', localPath: 'C:/repo', state: 'modified', error: null, status: {
  branch: 'feature/ação', isDetached: false, hasCommits: true, changedFiles: 9, modified: 1, added: 2, deleted: 3, renamed: 4,
  untracked: 5, conflicted: 6, staged: 7, unstaged: 8,
} };

it('renders the repository, branch and every file counter', () => {
  wrapper = mount(RepositoryStatusCard, { props: { repository } });
  expect(wrapper.get('h2').text()).toBe('Repo');
  expect(wrapper.get('.repository-location').text()).toBe('C:/repo');
  expect(wrapper.get('.git-branch').text()).toContain('feature/ação');
  expect(wrapper.findAll('dd').map(item => item.text())).toEqual(['9', '1', '2', '3', '4', '5', '6', '7', '8']);
  expect(wrapper.text()).not.toContain('ainda não possui commits');
});

it('distinguishes an unborn branch from detached HEAD and updates translations', async () => {
  wrapper = mount(RepositoryStatusCard, { props: { repository: { ...repository, state: 'clean', status: { ...repository.status!, hasCommits: false } } } });
  expect(wrapper.text()).toContain('ainda não possui commits');
  expect(wrapper.get('.git-state').text()).toBe('Limpo');
  await wrapper.setProps({ repository: { ...repository, state: 'conflicted', status: { ...repository.status!, isDetached: true } } });
  useLanguageStore().setLanguage('en');
  await wrapper.vm.$nextTick();
  expect(wrapper.get('.git-state').text()).toBe('Conflicted');
  expect(wrapper.get('.git-branch').text()).toContain('Detached HEAD');
});

it('shows a localized unavailable state without misleading zero counts', async () => {
  wrapper = mount(RepositoryStatusCard, { props: { repository: { ...repository, state: 'unavailable', status: null, error: 'Diretório movido' } } });
  expect(wrapper.get('[role="alert"]').text()).toBe('Diretório movido');
  expect(wrapper.get('.git-state').text()).toBe('Indisponível');
  expect(wrapper.find('dl').exists()).toBe(false);
  await wrapper.setProps({ repository: { ...repository, state: 'unavailable', status: null, error: null } });
  expect(wrapper.get('[role="alert"]').text()).toBe('Não foi possível consultar este repositório.');
});
