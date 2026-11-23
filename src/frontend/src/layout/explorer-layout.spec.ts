import { afterEach, beforeEach, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import { createMemoryHistory, createRouter } from 'vue-router';
import AppHeader from './app-header/AppHeader.vue';
import ExplorerSidebar from './explorer-sidebar/ExplorerSidebar.vue';
import OperationsDrawer from './operations-drawer/OperationsDrawer.vue';
import ApplicationLayout from './application-layout/ApplicationLayout.vue';
import SettingsPage from '../features/settings/SettingsPage.vue';
import { useExplorerStore } from '../core/catalog/explorer.store';
import { useExplorerPreferences } from '../core/preferences/explorer-preferences.store';
import { CatalogApiService } from '../core/catalog/catalog-api.service';
let wrapper: VueWrapper | undefined;
beforeEach(()=>{localStorage.clear();setActivePinia(createPinia());});afterEach(()=>wrapper?.unmount());
const links={RouterLink:{props:['to'],template:'<a :href="to"><slot /></a>'}};
it('keeps the existing transparent logo and allows collapsing Explorer',async()=>{
  wrapper=mount(AppHeader,{global:{stubs:links}});expect(wrapper.get('img').attributes('src')).toContain('Logo_GitCosmos.png');await wrapper.get('.explorer-toggle').trigger('click');expect(useExplorerPreferences().collapsed).toBe(true);expect(wrapper.get('.settings-link').attributes('href')).toBe('/settings');
});
it('expands workspaces, shows ungrouped repositories and offers retry and operations',async()=>{
  const store=useExplorerStore();store.index={workspaces:[{id:1,name:'Team'}],repositories:[{id:2,name:'Repo',localPath:'C:/repo',workspaceIds:[1],state:'unknown',status:null,error:null},{id:3,name:'Solo',localPath:'C:/solo',workspaceIds:[],state:'unknown',status:null,error:null}]};
  const refresh=vi.spyOn(store,'refresh').mockResolvedValue();wrapper=mount(ExplorerSidebar,{global:{stubs:links}});
  expect(wrapper.get('a[href="/repositories/3"]').text()).toContain('Solo');await wrapper.get('.tree-toggle').trigger('click');expect(wrapper.find('a[href="/workspaces/1/repositories/2"]').exists()).toBe(false);await wrapper.get('.tree-toggle').trigger('click');expect(wrapper.get('a[href="/workspaces/1/repositories/2"]').text()).toBe('Repo');
  await wrapper.get('.operations-toggle').trigger('click');expect(useExplorerPreferences().operationsOpen).toBe(true);
  store.loading=true;store.error='Offline';await flushPromises();expect(wrapper.text()).toContain('Carregando catálogo');await wrapper.get('.explorer-feedback button').trigger('click');expect(refresh).toHaveBeenCalled();
  store.loading=false;store.error='';store.index={workspaces:[],repositories:[]};useExplorerPreferences().collapsed=true;await flushPromises();expect(wrapper.text()).toContain('Nenhum workspace cadastrado');expect(wrapper.text()).toContain('Nenhum repositório sem workspace');
});
it('does not fabricate operation results and can close the route-independent drawer',async()=>{
  const preferences=useExplorerPreferences();wrapper=mount(OperationsDrawer);expect(wrapper.find('section').exists()).toBe(false);preferences.operationsOpen=true;await flushPromises();expect(wrapper.text()).toContain('Nenhuma operação em lote');await wrapper.get('button').trigger('click');expect(preferences.operationsOpen).toBe(false);
});
it('offers actual theme, language, width and connection settings',async()=>{
  wrapper=mount(SettingsPage,{global:{stubs:links}});await wrapper.get('input[type="range"]').setValue('320');expect(useExplorerPreferences().sidebarWidth).toBe(320);expect(wrapper.get('a').attributes('href')).toBe('/connection');
});
it('resolves breadcrumbs from actual catalog data and preserves shell state across routes',async()=>{
  const index={workspaces:[{id:1,name:'Team'}],repositories:[{id:2,name:'Repo',localPath:'C:/repo',workspaceIds:[1],state:'unknown' as const,status:null,error:null}]};
  vi.spyOn(CatalogApiService.prototype,'getExplorer').mockResolvedValue({success:true,result:index,errors:[],messages:[]});
  const blank={emits:['catalog-changed'],template:'<button class="catalog-change-probe" @click="$emit(\'catalog-changed\')">change</button>'};const router=createRouter({history:createMemoryHistory(),routes:[{path:'/',component:blank},{path:'/repositories',component:blank},{path:'/repositories/:repositoryId',component:blank},{path:'/workspaces',component:blank},{path:'/workspaces/:id',component:blank},{path:'/workspaces/:workspaceId/repositories/:repositoryId',component:blank},{path:'/settings',component:blank},{path:'/connection',component:blank}]});
  await router.push('/');await router.isReady();wrapper=mount(ApplicationLayout,{global:{plugins:[router]}});await flushPromises();expect(wrapper.get('.breadcrumbs').text()).toContain('Mission Control');
  await wrapper.get('.catalog-change-probe').trigger('click'); await flushPromises();
  for(const [path,label]of[['/repositories','Todos os repositórios'],['/repositories/2','Repo'],['/repositories/999','Repositório'],['/workspaces','Gerenciar workspaces'],['/workspaces/1','Team'],['/workspaces/1/repositories/2','Repo'],['/workspaces/999','Workspaces'],['/settings','Configurações'],['/connection','Conexão']]){await router.push(path!);await flushPromises();expect(wrapper.get('.breadcrumbs').text()).toContain(label!);}
  const store=useExplorerStore();store.loading=true;await flushPromises();expect(wrapper.get('footer').text()).toContain('Consultando');store.loading=false;store.error='Offline';await flushPromises();expect(wrapper.get('footer').text()).toContain('Indisponível');
});
